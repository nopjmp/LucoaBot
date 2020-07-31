using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    [RequireGuild]
    [RequireBotPermissions(Permissions.ManageRoles & Permissions.SendMessages)]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public class SelfRoleModule : BaseCommandModule
    {
        private readonly DatabaseContext _databaseContext;

        public SelfRoleModule(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        [Command("roles")]
        [Aliases("lsar")]
        [Description("Lists self assignable roles.")]
        public async Task ListRolesAsync(CommandContext context) //(bool textOnly = false)
        {
            var selfRoles = (await _databaseContext.SelfRoles.AsNoTracking()
                    .Where(r => r.GuildId == context.Guild.Id)
                    .ToListAsync())
                .GroupBy(r => r.Category ?? "default")
                .OrderByDescending(group => group.Key == "default") // sort default to the front
                .ThenBy(group => group.Key)
                .ToImmutableList();

            var embedBuilder = new DiscordEmbedBuilder
            {
                Title = "Self Assignable Roles"
            };

            var inline = !selfRoles.Select(x => x.Count()).Any(c => c > 5);

            foreach (var group in selfRoles)
                embedBuilder.AddField(group.Key,
                    string.Join(" ",
                        group.Select(r => context.Guild.GetRole(r.RoleId)
                            .Mention)),
                    inline);

            if (!embedBuilder.Fields.Any())
                embedBuilder.Description = "There are no self assignable roles.";

            await context.RespondAsync(embed: embedBuilder.Build());
        }

        [Command("addrole")]
        [Aliases("asar")]
        [Description("Adds a role for self-assignment")]
        [RequireUserPermissions(Permissions.ManageRoles)]
        public async Task AddRoleAsync(CommandContext context, DiscordRole role, string category = null)
        {
            var selfRoleCheck = await _databaseContext.SelfRoles
                .Where(r => r.GuildId == context.Guild.Id && r.RoleId == role.Id)
                .AnyAsync();

            if (selfRoleCheck)
            {
                await context.RespondAsync($"**{role.Name}** is already a self assignable role.");
                return;
            }


            var selfRoleEntry = new SelfRole
            {
                Category = category,
                GuildId = context.Guild.Id,
                RoleId = role.Id
            };

            _databaseContext.Add(selfRoleEntry);
            await _databaseContext.SaveChangesAsync();

            await context.RespondAsync(
                $"**{role.Name}** is now self assignable in category {category ?? "default"}.");
        }

        [Command("removerole")]
        [Aliases("rsar")]
        [Description("Removes a role from self assignment")]
        [RequireUserPermissions(Permissions.ManageRoles)]
        public async Task RemoveRoleAsync(CommandContext context, DiscordRole role)
        {
            var selfRoleEntry = await _databaseContext.SelfRoles
                .Where(r => r.GuildId == context.Guild.Id && r.RoleId == role.Id)
                .FirstOrDefaultAsync();

            if (selfRoleEntry == null)
            {
                await context.RespondAsync($"**{role.Name}** not found as a self-assignable role on this server.");
                return;
            }

            _databaseContext.SelfRoles.Remove(selfRoleEntry);
            await _databaseContext.SaveChangesAsync();

            await context.RespondAsync($"**{role.Name}** is no longer self assignable.");
        }

        [Command("iam")]
        [Aliases("r")]
        [Description("Assigns the request role to the caller, if allowed")]
        public async Task IamAsync(CommandContext context, params string[] roleName)
        {
            var roleNameString = string.Join(' ', roleName);
            var role = context.Guild.Roles.Values.FirstOrDefault(xr =>
                xr.Name.Equals(roleNameString, StringComparison.InvariantCultureIgnoreCase));

            if (role == null)
            {
                await context.RespondAsync($"**{roleNameString}** is not a valid role.");
                return;
            }

            var member = context.Member;
            if (!member.Roles.Contains(role))
            {
                var selfRoleEntry = await _databaseContext.SelfRoles.AsNoTracking()
                    .Where(x => x.GuildId == context.Guild.Id && x.RoleId == role.Id)
                    .FirstOrDefaultAsync();

                if (selfRoleEntry != null)
                {
                    if (selfRoleEntry.Category != null && selfRoleEntry.Category != "default")
                    {
                        var roles = await _databaseContext.SelfRoles.AsNoTracking()
                            .Where(r => r.GuildId == context.Guild.Id && r.Category == selfRoleEntry.Category)
                            .Select(r => r.RoleId)
                            .ToListAsync();

                        var removeList = member.Roles.Where(r => roles.Contains(r.Id))
                            .Select(r => context.Guild.GetRole(r.Id))
                            .Select(r => member.RevokeRoleAsync(r))
                            .ToList();

                        await Task.WhenAll(removeList);
                    }

                    // TODO: use ReplaceRolesAsync
                    await member.GrantRoleAsync(role);
                    await context.RespondAsync($"{context.User.Mention} now has the role **{role.Name}**");
                }
                else
                {
                    await context.RespondAsync($"**{role.Name}** is not a self-assignable role.");
                }
            }
            else
            {
                await context.RespondAsync($"{context.User.Mention}... You already have **{role.Name}**.");
            }
        }

        [Command("iamnot")]
        [Aliases("iamn", "nr")]
        [Description("Removes the requested role from the caller, if allowed")]
        public async Task IamNotAsync(CommandContext context, params string[] roleName)
        {
            var roleNameString = string.Join(' ', roleName);
            var role = context.Guild.Roles.Values.FirstOrDefault(xr =>
                xr.Name.Equals(roleNameString, StringComparison.InvariantCultureIgnoreCase));

            if (role == null)
            {
                await context.RespondAsync($"**{roleNameString}** is not a valid role.");
                return;
            }

            var member = context.Member;
            if (member.Roles.Contains(role))
            {
                var selfRoleExists = await _databaseContext.SelfRoles.AsNoTracking()
                    .Where(x => x.GuildId == context.Guild.Id && x.RoleId == role.Id)
                    .AnyAsync();

                if (selfRoleExists)
                {
                    await member.RevokeRoleAsync(role);
                    await context.RespondAsync($"{context.User.Mention} no longer has **{role.Name}**");
                }
                else
                {
                    await context.RespondAsync($"**{role.Name}** is not a self-assignable role.");
                }
            }
            else
            {
                await context.RespondAsync($"{context.User.Mention}... You do not have **{role.Name}**.");
            }
        }
    }
}