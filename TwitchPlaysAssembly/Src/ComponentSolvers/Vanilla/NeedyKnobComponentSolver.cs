using System.Collections;

public class NeedyKnobComponentSolver : ComponentSolver
{
	public NeedyKnobComponentSolver(TwitchModule module) :
		base(module)
	{
		_pointingKnob = ((NeedyKnobComponent) module.BombComponent).PointingKnob;
		ModInfo = ComponentSolverFactory.GetModuleInfo("NeedyKnob", "!{0} rotate 3, !{0} turn 3 [ダイヤルを3回回す。一回に付き1/4回転する]");
		ModInfo.moduleTranslatedName = "ダイヤル";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Knob%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%83%80%E3%82%A4%E3%83%A4%E3%83%AB).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		string[] commandParts = inputCommand.ToLowerInvariant().Trim().Split(' ');

		if (commandParts.Length != 2)
		{
			yield break;
		}

		if (!commandParts[0].EqualsAny("rotate", "turn"))
		{
			yield break;
		}

		if (!int.TryParse(commandParts[1], out int totalTurnCount))
		{
			yield break;
		}

		totalTurnCount %= 4;

		yield return "rotate";

		for (int turnCount = 0; turnCount < totalTurnCount; ++turnCount)
		{
			yield return "trycancel";
			yield return DoInteractionClick(_pointingKnob);
		}
	}

	private readonly PointingKnob _pointingKnob;
}
