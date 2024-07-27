using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SimonComponentSolver : ComponentSolver
{
	public SimonComponentSolver(TwitchModule module) :
		base(module)
	{
		_buttons = ((SimonComponent) module.BombComponent).buttons;
		ModInfo = ComponentSolverFactory.GetModuleInfo("Simon", "!{0} press red green blue yellow, !{0} press rgby [その色のボタンを押す。R=赤、G=緑、B=青、Y=黄] | それまでのステージの入力も行わなければならない。");
		ModInfo.moduleTranslatedName = "サイモンゲーム";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Simon%20Says%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%82%B5%E3%82%A4%E3%83%A2%E3%83%B3%E3%82%B2%E3%83%BC%E3%83%A0).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		if (!inputCommand.StartsWith("press ", StringComparison.InvariantCultureIgnoreCase))
			yield break;
		inputCommand = inputCommand.Substring(6);

		string sequence = "pressing ";
		foreach (Match move in Regex.Matches(inputCommand, @"(\b(red|blue|green|yellow)\b|[rbgy])", RegexOptions.IgnoreCase))
		{
			SimonButton button = _buttons[ButtonIndex[move.Value.Substring(0, 1).ToLowerInvariant()]];

			if (button == null) continue;
			yield return move.Value;
			sequence += move.Value + " ";

			if (CoroutineCanceller.ShouldCancel)
			{
				CoroutineCanceller.ResetCancel();
				yield break;
			}

			yield return DoInteractionClick(button, sequence);
		}
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		while (!Module.BombComponent.IsActive) yield return true;
		while (!Module.Solved)
		{
			int index = ((SimonComponent) Module.BombComponent).GetNextIndexToPress();
			yield return DoInteractionClick(_buttons[index]);
		}
	}

	private static readonly Dictionary<string, int> ButtonIndex = new Dictionary<string, int>
	{
		{"r", 0}, {"b", 1}, {"g", 2}, {"y", 3}
	};

	private readonly SimonButton[] _buttons;
}
