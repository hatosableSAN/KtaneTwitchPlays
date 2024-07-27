using System;
using System.Collections;
using System.Text.RegularExpressions;
using Assets.Scripts.Rules;

public class KeypadComponentSolver : ComponentSolver
{
	public KeypadComponentSolver(TwitchModule module) :
		base(module)
	{
		_buttons = ((KeypadComponent) module.BombComponent).buttons;
		ModInfo = ComponentSolverFactory.GetModuleInfo("Keypad", "!{0} press 3 1 2 4 [キーをその順番に押す] | ボタンは1=左上、2=右上、3=左下、4=右下");
		ModInfo.moduleTranslatedName = "キーパッド";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Keypad%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%82%AD%E3%83%BC%E3%83%91%E3%83%83%E3%83%89).html";

	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		if (!inputCommand.StartsWith("press ", StringComparison.InvariantCultureIgnoreCase))
			yield break;
		inputCommand = inputCommand.Substring(6);

		foreach (Match buttonIndexString in Regex.Matches(inputCommand, @"[1-4]"))
		{
			if (!int.TryParse(buttonIndexString.Value, out int buttonIndex))
				continue;

			buttonIndex--;

			if (buttonIndex < 0 || buttonIndex >= _buttons.Length) continue;
			if (_buttons[buttonIndex].IsStayingDown)
				continue;

			yield return buttonIndexString.Value;
			yield return "trycancel";
			yield return DoInteractionClick(_buttons[buttonIndex]);
		}
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		while (!Module.Solved)
			yield return DoInteractionClick(_buttons[
				RuleManager.Instance.KeypadRuleSet.GetNextSolutionIndex(((KeypadComponent) Module.BombComponent).pListIndex,
					_buttons)]);
	}

	private readonly KeypadButton[] _buttons;
}
