using UnityEngine;

namespace Airstrike
{
    internal static class UiLanguage
    {
        private const string Key="Skybreach.Language";
        private static bool? chinese;
        private static Font chineseFont;
        public static bool Chinese
        {
            get
            {
                chinese??=!Application.isBatchMode && PlayerPrefs.GetInt(Key,Application.systemLanguage is SystemLanguage.Chinese or
                    SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional?1:0)==1;
                return chinese.Value;
            }
        }
        public static string Text(string english,string simplifiedChinese) => Chinese?simplifiedChinese:english;
        public static string Mode(BalanceMode mode) => mode==BalanceMode.PC?Text("PC EDITION","当前版本"):Text("CLASSIC RULES","原版数值");

        public static void Toggle()
        {
            chinese=!Chinese;
            if(Application.isBatchMode) return;
            PlayerPrefs.SetInt(Key,Chinese?1:0);
            PlayerPrefs.Save();
        }

        public static Font FontForLanguage()
        {
            if(Chinese)
            {
                if(chineseFont==null)
                    chineseFont=Font.CreateDynamicFontFromOSFont(
                        new[] {"Microsoft YaHei UI","Microsoft YaHei","SimHei","SimSun","Noto Sans CJK SC","WenQuanYi Zen Hei"},24);
                if(chineseFont!=null) return chineseFont;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static string Wave(string wave) => wave switch
        {
            "FIRST CONTACT"=>Text(wave,"初次交锋"),
            "FAST MOVERS"=>Text(wave,"高速来袭"),
            "ROCKET LOCK"=>Text(wave,"火箭锁定"),
            "HARD TARGETS"=>Text(wave,"装甲目标"),
            "CROSS FIRE"=>Text(wave,"交叉火力"),
            "HOLD THE SECTOR"=>Text(wave,"坚守阵地"),
            "ENDLESS SKY"=>Text(wave,"无尽空袭"),
            _=>wave
        };
    }
}
