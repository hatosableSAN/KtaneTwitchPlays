using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TheUltracubeTranslationShim : TranslationShim
{
	public TheUltracubeTranslationShim(TwitchModule module) : base(module, inputTranslation, null) { }

	internal static TranslationBuilder inputTranslation = Build()
		.Add(new Regex("　+"), " ")
		.Add(new Regex("ー"), "-")
		.Add("右", "right")
		.Add("左", "left")
		.Add("前", "front")
		.Add("後", "back")
		.Add("上", "top")
		.Add("下", "bottom")
		.Add("甲", "zig")
		.Add("乙", "zag")
		.Add("阿", "ping")
		.Add("吽", "pong");
}