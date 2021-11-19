using System;

namespace LucoaBot.Data.Entities
{
    public enum UserAction
    {
        Join,
        Left
    }

    public static class UserActionExtension
    {
        public static string ToFriendly(this UserAction userAction)
        {
            return userAction switch
            {
                UserAction.Join => "joined",
                UserAction.Left => "left",
                _ => throw new ArgumentOutOfRangeException(nameof(userAction), userAction, null)
            };
        }
    }
}