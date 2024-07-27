using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static Repository;

namespace TwitchPlaysAssembly.Src.Helpers
{
	class TranslationInfo
	{
		public static IEnumerator TranslateComponents()
		{
			yield return LoadData();

			DebugHelper.Log("Start translation");
			var reManual = new Regex(@"^ translated \(日本語 — ([^)]+)\)( \([^)]+\))?");
			var modInfos = ComponentSolverFactory.GetModuleInformation().ToList();
			var validModules = Modules.Where(module =>
			{
				if (module.SteamID == null || module.Type == "Widget" || module.Sheets == null)
					return false;

				var current = modInfos.Find(modInfo => modInfo.moduleID == module.ModuleID);
				return current == null || current.moduleTranslatedName == null || current.moduleTranslatedName.Length == 0;
			}).ToArray();


			foreach (var module in validModules)
			{
				var translatedSheet = module.Sheets.Find(sheet => reManual.IsMatch(sheet));
				if (translatedSheet == null) continue;
				var match = reManual.Match(translatedSheet);
				var modInfo = modInfos.Find(m => m.moduleID == module.ModuleID);
				if (!modInfo.manualCodeOverride)
				{
					modInfo.manualCodeOverride = true;
					modInfo.manualCode = $"https://ktane.timwi.de/HTML/{Uri.EscapeDataString(module.Name + match.Groups[0].Value)}.html";
					ModuleData.DataHasChanged = true;
				}
				if(modInfo.moduleTranslatedName != match.Groups[1].Value) {
					modInfo.moduleTranslatedName = match.Groups[1].Value;
					ModuleData.DataHasChanged = true;
				}
			}
			ModuleData.WriteDataToFile();
		}
	}
}
