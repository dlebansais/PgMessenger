namespace PgMessenger
{
    public class CharacterSetting
    {
        #region Init
        public CharacterSetting(string name, string guildName, bool isAutoUpdated, string password)
        {
            Name = name;
            GuildName = guildName;
            IsAutoUpdated = isAutoUpdated;
            Password = password;
        }
        #endregion

        #region Properties
        public string Name { get; set; }
        public string GuildName { get; set; }
        public bool IsAutoUpdated { get; set; }
        public string Password { get; set; }
        #endregion

        #region Overrides
        public override string ToString()
        {
            return Name + ", guild: " + GuildName + ", " + (IsAutoUpdated ? "Auto Update" : "Password: " + Password);
        }
        #endregion
    }
}
