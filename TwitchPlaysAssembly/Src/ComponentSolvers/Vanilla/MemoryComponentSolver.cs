using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Rules;

public class MemoryComponentSolver : ComponentSolver
{
	public MemoryComponentSolver(TwitchModule module) :
		base(module)
	{
		_buttons = ((MemoryComponent) module.BombComponent).Buttons;
		ModInfo = ComponentSolverFactory.GetModuleInfo("Memory", "!{0} position 2, !{0} pos 2, !{0} p 2 [二番目のボタンを押す] | !{0} label 3, !{0} lab 3, !{0} l 3 [3と書かれたボタンを押す]");
		ModInfo.moduleTranslatedName = "記憶";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Memory%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E8%A8%98%E6%86%B6).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		inputCommand = inputCommand.Trim();
		string[] commandParts = inputCommand.ToLowerInvariant().Split(' ');

		if (commandParts.Length != 2)
			yield break;

		if (!int.TryParse(commandParts[1], out int buttonNumber))
			yield break;

		if (buttonNumber < 1 || buttonNumber > 4) yield break;
		if (commandParts[0].EqualsAny("position", "pos", "p"))
		{
			yield return "position";

			yield return DoInteractionClick(_buttons[buttonNumber - 1]);
		}
		else if (commandParts[0].EqualsAny("label", "lab", "l"))
		{
			foreach (KeypadButton button in _buttons)
			{
				if (!button.Text.text.Equals(buttonNumber.ToString())) continue;
				yield return "label";
				yield return DoInteractionClick(button);
				break;
			}
		}
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		MemoryComponent mc = (MemoryComponent) Module.BombComponent;
		while (!Module.BombComponent.IsActive) yield return true;
		while (!Module.Solved)
		{
			while (!mc.IsInputValid) yield return true;
			List<Rule> ruleList = RuleManager.Instance.MemoryRuleSet.RulesDictionary[mc.CurrentStage];
			yield return DoInteractionClick(_buttons[RuleManager.Instance.MemoryRuleSet.ExecuteRuleList(mc, ruleList)]);
		}
	}

	private readonly KeypadButton[] _buttons;
}
