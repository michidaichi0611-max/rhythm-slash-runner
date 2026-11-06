namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Round 36: switches for parts that are not ready for players. With <see cref="ComingSoon"/> off, the event button,
    /// the shop's "特別" tab and the paid drink packs (while the store is not connected) are hidden instead of saying "準備中".
    /// The screens are still built (and captured by the automation), so turning it on brings them back unchanged.
    /// </summary>
    public static class Features
    {
        public const bool ComingSoon = false;

        /// <summary>The privacy policy page (store/privacy_policy.html, once it is online). Empty: no button in the license screen.</summary>
        public const string PrivacyPolicyUrl = "";
    }
}
