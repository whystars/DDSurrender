// Config.cs
using Exiled.API.Interfaces;
using System.ComponentModel;

namespace DDSurrender
{
    public class SurrenderConfig : IConfig
    {
        [Description("是否启用插件")]
        public bool IsEnabled { get; set; } = true;

        [Description("调试模式（默认关闭）")]
        public bool Debug { get; set; } = false;

        [Description("彩蛋(反复投降提示文字):")]
        public string SuAgain { get; set; } = "<color=#FFFF66>⚠️ 重复投降，视为反水，将在114514秒后自动处死（doge） </color>";

        [Description("投降提示:")]
        public string SuSuccess { get; set; } = "<color=#66FF66>✅ 投降成功！请勿重复投降，否则有惊喜！！！！</color>";

        [Description("投降失败, 原因——造反了, 提示内容:")]
        public string Zaofan { get; set; } = "<color=#FF6666>❌ 都早饭了还想投降?! 不可以的哦~~~</color>";

        [Description("投降失败, 不是DD, 提示内容:")]
        public string NotClassD { get; set; } = "<color=#FF6666>❌ 仅D级人员可投降╮(╯▽╰)╭</color>";

        [Description("投降失败提示, 原因不明:")]
        public string FailedCausedUnknown { get; set; } = "<color=#FF6666>❌ 投降失败! 原因未知, 请联系管理员报告此问题。</color>";

        [Description("投降D标识:")]
        public string SuDD { get; set; } = "【已投降】";

        [Description("早饭D标识:")]
        public string ZFDD { get; set; } = "【造反中】";

        [Description("指令描述(控制台.help之后的指令描述信息):")]
        public string command_description { get; set; } = "[DDSurrender] D级投降插件, DD投降指令, 仅限D级人员使用";

        [Description("DD开局广播内容:")]
        public string BroadcastForDD { get; set; } = "<size=25>你成为了D级人员，很遗憾你不受到规则保护，但你可以在控制台输入 .tx 进行投降。\n投降后不会被基金会人员伤害，但混沌分裂者可以攻击你（你也可反击）</size>";
    }
}