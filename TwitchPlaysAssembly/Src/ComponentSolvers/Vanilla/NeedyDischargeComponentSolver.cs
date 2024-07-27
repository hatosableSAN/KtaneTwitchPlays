using System;
using System.Collections;

public class NeedyDischargeComponentSolver : ComponentSolver
{
	public NeedyDischargeComponentSolver(TwitchModule module) :
		base(module)
	{
		_dischargeButton = ((NeedyDischargeComponent) module.BombComponent).DischargeButton;
		ModInfo = ComponentSolverFactory.GetModuleInfo("NeedyCapacitor", "!{0} hold 7 [7秒間長押しする]");	 
		ModInfo.moduleTranslatedName = "コンデンサー";
		ModInfo.manualCode = "https://ktane.timwi.de/HTML/Capacitor%20Discharge%20translated%20(%E6%97%A5%E6%9C%AC%E8%AA%9E%20%E2%80%94%20%E3%82%B3%E3%83%B3%E3%83%87%E3%83%B3%E3%82%B5%E3%83%BC).html";
	}

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		string[] commandParts = inputCommand.Trim().Split(' ');

		if (commandParts.Length != 2 || !commandParts[0].Equals("hold", StringComparison.InvariantCultureIgnoreCase))
			yield break;

		if (!float.TryParse(commandParts[1], out float holdTime)) yield break;

		yield return "hold";

		if (holdTime > 9) yield return "elevator music";

		DoInteractionStart(_dischargeButton);
		yield return new WaitForSecondsWithCancel(holdTime);
		DoInteractionEnd(_dischargeButton);
	}

	private readonly SpringedSwitch _dischargeButton;
}
