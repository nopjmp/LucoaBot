using Discord;
using Discord.Commands;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LucoaBot.Commands
{
    [Name("Self Role")]
    [RequireContext(ContextType.Guild)]
    public class SelfRoleModule : ModuleBase<SocketCommandContext>
    {
        private readonly DatabaseContext context;
        public SelfRoleModule(DatabaseContext context)
        {
            this.context = context;
        }

        [Command("roles")]
        [Alias("lsar")]
        [Summary("Lists self assignable roles.")]
        public async Task ListRolesAsync(bool textOnly = false)
        {
            var selfRoles = (await context.SelfRoles
                .Where(r => r.GuildId == Context.Guild.Id)
                .ToListAsync())
                .GroupBy(r => r.Category ?? "default")
                .OrderBy(group => group.Key);

            var embedBuilder = new EmbedBuilder()
            {
                Title = "Self Assignable Roles"
            };
            foreach (var group in selfRoles)
            {
                embedBuilder.AddField(group.Key, string.Join(" ", group.Select(r => Context.Guild.GetRole(r.RoleId).Mention)), true);
            }

            // sort default to the front
            var fb = embedBuilder.Fields.Find(fb => fb.Name == "default");
            if (fb != null)
            {
                embedBuilder.Fields.Remove(fb);
                embedBuilder.Fields.Insert(0, fb);
            }

            if (!embedBuilder.Fields.Any())
                embedBuilder.Description = "There are no self assignable roles.";

            await ReplyAsync(embed: embedBuilder.Build());
        }

        [Command("addrole")]
        [Alias("asar")]
        [Summary("Adds a role for self-assignment")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task<RuntimeResult> AddRoleAsync(IRole role, string category = null)
        {
            var selfRoleCheck = await context.SelfRoles
                .Where(r => r.GuildId == Context.Guild.Id && r.RoleId == role.Id)
                .AnyAsync();

            if (selfRoleCheck)
                return CommandResult.FromError($"**{role.Name}** is already a self assignable role.");

            var selfRoleEntry = new SelfRole()
            {
                Category = category,
                GuildId = Context.Guild.Id,
                RoleId = role.Id,
            };

            context.Add(selfRoleEntry);
            await context.SaveChangesAsync();

            return CommandResult.FromSuccess($"**{role.Name}** is now self assignable in category {category ?? "default"}.");
        }

        [Command("removerole")]
        [Alias("rsar")]
        [Summary("Removes a role from self assignment")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task<RuntimeResult> RemoveRoleAsync(IRole role)
        {
            var selfRoleEntry = await context.SelfRoles
                .Where(r => r.GuildId == Context.Guild.Id && r.RoleId == role.Id)
                .FirstOrDefaultAsync();

            if (selfRoleEntry != null)
            {
                context.SelfRoles.Remove(selfRoleEntry);
                await context.SaveChangesAsync();

                return CommandResult.FromSuccess($"**{role.Name}** is no longer self assignable.");
            }

            return CommandResult.FromError($"**{role.Name}** not found as a self-assignable role on this server.");
        }

        [Command("iam")]
        [Alias("r")]
        [Summary("Assigns the request role to the caller, if allowed")]
        public async Task<RuntimeResult> IamAsync(IRole role)
        {
            var guildUser = Context.User as IGuildUser;
            if (guildUser == null)
                return CommandResult.FromError($"Secret easter egg, please message the bot owner!!! 🤕");

            if (guildUser.RoleIds.Contains(role.Id))
                return CommandResult.FromError($"{Context.User.Mention}... You already have **{role.Name}**.");

            var selfRoleEntry = await context.SelfRoles
                .Where(x => x.GuildId == Context.Guild.Id && x.RoleId == role.Id)
                .FirstOrDefaultAsync();

            if (selfRoleEntry != null)
            {
                if (selfRoleEntry.Category != null && selfRoleEntry.Category != "default")
                {
                    var removeList = await context.SelfRoles
                        .Where(r => r.GuildId == Context.Guild.Id && r.Category == selfRoleEntry.Category && guildUser.RoleIds.Contains(r.RoleId))
                        .Select(r => Context.Guild.GetRole(r.RoleId))
                        .ToListAsync();

                    if (removeList.Any())
                        await guildUser.RemoveRolesAsync(removeList);
                }

                await guildUser.AddRoleAsync(role);
                return CommandResult.FromSuccess($"{Context.User.Mention} now has the role **{role.Name}**");
            }

            return CommandResult.FromError($"**{role.Name}** is not a self-assignable role.");
        }

        [Command("iamnot")]
        [Alias("iamn", "nr")]
        [Summary("Removes the requested role from the caller, if allowed")]
        public async Task<RuntimeResult> IamNotAsync(IRole role)
        {
            var guildUser = Context.User as IGuildUser;
            if (guildUser == null)
                return CommandResult.FromError($"Secret easter egg, please message the bot owner!!! 🤕");

            if (!guildUser.RoleIds.Contains(role.Id))
                return CommandResult.FromError($"{Context.User.Mention}... You do not have **{role.Name}**.");

            var selfRoleExists = await context.SelfRoles
                .Where(x => x.GuildId == Context.Guild.Id && x.RoleId == role.Id)
                .AnyAsync();

            if (selfRoleExists)
            {
                await guildUser.RemoveRoleAsync(role);

                return CommandResult.FromSuccess($"{Context.User.Mention} no longer has **{role.Name}**");
            }

            return CommandResult.FromError($"**{role.Name}** is not a self-assignable role.");
        }
    }
}
