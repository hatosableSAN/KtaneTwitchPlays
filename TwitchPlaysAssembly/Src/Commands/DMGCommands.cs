﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>DMGで使えるコマンド</summary>
/// <prefix>dmg </prefix>
public static class DMGCommands
{
	private static readonly Type pageNavigationType = ReflectionHelper.FindType("PageNavigation");

	private static Stack<KMSelectable> _backStack;
	private static KMSelectable CurrentPage => _backStack.Peek();

	/// <name>Run</name>
	/// <syntax>run</syntax>
	/// <summary>特定のテキストでDMGを実行する。トレーニングモードであるかAdmin権限所有者のみ利用できる。</summary>
	[Command("run (.+)")]
	public static IEnumerator Run(string user, [Group(1)] string text)
	{
		if (!UserAccess.HasAccess(user, AccessLevel.Admin, true))
		{
			if (!TwitchPlaySettings.data.EnableDMGForEveryone)
			{
				IRCConnection.SendMessage("「Admin」権限所持者のみDMGを利用できます。");
				yield break;
			}
			if (!OtherModes.TrainingModeOn)
			{
				IRCConnection.SendMessage("トレーニングモード以外では、「Admin」権限所持者のみDMGを利用できます。");
				yield break;
			}
		}

		var pageNavigation = UnityEngine.Object.FindObjectOfType(pageNavigationType);

		// pageNavigation could be null if this command is run multiple times in the setup room.
		if (pageNavigation == null)
			yield break;

		_backStack = pageNavigation.GetValue<Stack<KMSelectable>>("_backStack");

		while (!CurrentPage.name.EqualsAny("PageOne(Clone)", "Home(Clone)"))
		{
			KTInputManager.Instance.HandleCancel();
			yield return new WaitForSeconds(0.1f);
		}

		if (CurrentPage.name == "Home(Clone)")
		{
			var entryIndex = ReflectionHelper.FindType("PageManager")
				.GetValue<IList>("HomePageEntryList")
				.Cast<object>()
				.IndexOf(entry => entry.GetValue<string>("DisplayName") == "Dynamic Mission Generator");

			if (entryIndex == -1)
			{
				IRCConnection.SendMessage("DMGがインストールされていません。");
				yield break;
			}

			CurrentPage.Children[entryIndex].OnInteract();
			yield return new WaitForSeconds(0.1f);
		}

		CurrentPage.gameObject.Traverse<InputField>("Canvas", "InputField").text = text;
		yield return new WaitForSeconds(0.1f);
		CurrentPage.Children.First(button => button.name.Contains("Run")).OnInteract();
	}
}
