using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class ForgetMeNotTranslationShim : TranslationShim
{
	public ForgetMeNotTranslationShim(TwitchModule module) : base(module, null, _outputTranslation) { }

	internal static TranslationBuilder _outputTranslation = Build()
		.Add("OhMyDog Here we go!", "さあ行きますよ!")
		.Add("Maybe this time?", "これならどうでしょう?")
		.Add(new Regex(@"Kreygasm We did it .+!"), @"やりました!")
		.Add("Kappa Nope, just kidding.", "なんちゃって、嘘です")
		.Add("DansGame This isn't correct...", "なんか違いますね...")
		.Add("Correct digits entered:", "正しく入力された桁数:")
		.Add("Use either 'submit' or 'press' followed by a number sequence.", "submitかpressに続いて数字の列を入力してください。")
		.Add(new Regex(@"Invalid character in number sequence: '(.)'\.\nValid characters are 0 - 9, space, and comma\."), "数字の列に無効な文字がありました: $1\n有効な文字は0-9と、空白とコンマのみです")
		.Add("DansGame A little early, don't you think?", "ちょっと早すぎないですか?")
		.Add("NotLikeThis Too many digits submitted.", "入力された数字が多すぎます");

}