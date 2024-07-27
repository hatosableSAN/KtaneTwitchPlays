using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class ForgetItNotTranslationShim : TranslationShim
{
	public ForgetItNotTranslationShim(TwitchModule module) : base(module, null, _outputTranslation) { }

	internal static TranslationBuilder _outputTranslation = Build()
		.Add("Let's finish this!", "終わらせましょう!")
		.Add("This better be it!", "これなら合ってるはずです!")
		.Add("DansGame Oh come on!", "そんなはずは!")
		.Add("DansGame The end is right there!! Why!?", "あとちょっとなのに!")
		.Add("DansGame I'm not done yet!?", "まだ終わってないの?")
		.Add("DansGame This isn't correct...", "なんか違いますね...")
		.Add("Got to get this out now!", "今すぐ入力しないと!")
		.Add("I'm hurrying already!", "急がないと!")
		.Add("PogChamp PraiseIt And that's how it's done!", "これで爆弾解除完了!")
		.Add("Aced it!", "これで完了!")
		.Add("Too easy.", "簡単でしたね!")
		.Add("Kreygasm PraiseIt We're done!", "これで爆弾解除完了!")
		.Add("Kreygasm It's done!", "これで完了!")
		.Add(new Regex(@"A total of (.+) digit\(s\) were entered correctly when the strike occured."), "正しく入力された桁数: $1")
		.Add("Your command is invalid. The command must start with \"press\" or \"submit\" followed by a string of digits.", "submitかpressに続いて数字の列を入力してください。")
		.Add(new Regex(@"Your command is invalid\. The character ""(.)"" is invalid\."), "数字の列に無効な文字がありました: $1\n有効な文字は0-9と、空白とコンマのみです")
		.Add("Too early. Don't try to press a digit until this module is ready for input.", "モジュールが入力待機状態になるまで入力できません。")
		.Add("Your command has too many digits. Please reinput the command with fewer digits.", "入力された数字が多すぎます");

}