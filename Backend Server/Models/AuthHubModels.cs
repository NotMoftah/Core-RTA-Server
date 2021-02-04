using System;

namespace ShedApp.Models
{
    public class PlayerInfo
    {
        public string username { get; set; }
        public string password { get; set; }

    }

    public class LoginRequest
    {
        public string token { get; set; }
        public string password { get; set; }
    }

    public class PlayerStatus
    {
        public bool isInGame { get; set; }
        public bool isLoggedIn { get; set; }
        public object player { get; set; }
    }
}