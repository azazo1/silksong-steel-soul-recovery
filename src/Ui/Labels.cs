using System;
using SteelSoulRecovery.Config;
using TeamCherry.Localization;

namespace SteelSoulRecovery.Ui
{
    // 恢复选项的按钮文字.
    internal static class Labels
    {
        private const string ChineseLabel = "恢复钢魂存档";

        private const string EnglishLabel = "Restore Steel Soul Save";

        internal static string RecoveryOption(RecoveryConfig config)
        {
            string configured = config.ButtonLabel.Value;
            if (!string.IsNullOrEmpty(configured) && configured.Trim().Length > 0)
            {
                return configured;
            }

            return IsChinese() ? ChineseLabel : EnglishLabel;
        }

        private static bool IsChinese()
        {
            try
            {
                switch (Language.CurrentLanguage())
                {
                    case LanguageCode.ZH:
                    case LanguageCode.ZH_CN:
                    case LanguageCode.ZH_TW:
                    case LanguageCode.ZH_HK:
                    case LanguageCode.ZH_SG:
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                // 本地化还没准备好时不要因为这个挂掉, 退回英文.
                return false;
            }
        }
    }
}
