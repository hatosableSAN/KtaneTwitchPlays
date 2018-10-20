﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public class Factory : GameRoom
{
	private readonly bool _finiteMode;
	private readonly bool _infiniteMode;
	private readonly bool _zenMode = false; //For future use.

	public static Type FactoryType()
	{
		if (_factoryType != null) return _factoryType;

		_factoryType = ReflectionHelper.FindType("FactoryAssembly.FactoryRoom");
		if (_factoryType == null)
			return null;

		_factoryBombType = ReflectionHelper.FindType("FactoryAssembly.FactoryBomb");
		_internalBombProperty = _factoryBombType.GetProperty("InternalBomb", BindingFlags.NonPublic | BindingFlags.Instance);
		_bombEndedProperty = _factoryBombType.GetProperty("Ended", BindingFlags.NonPublic | BindingFlags.Instance);

		_factoryModeType = ReflectionHelper.FindType("FactoryAssembly.FactoryGameMode");
		_destroyBombMethod = _factoryModeType.GetMethod("DestroyBomb", BindingFlags.NonPublic | BindingFlags.Instance);

		_factoryStaticModeType = ReflectionHelper.FindType("FactoryAssembly.StaticMode");
		_factoryFiniteModeType = ReflectionHelper.FindType("FactoryAssembly.FiniteSequenceMode");
		_factoryInfiniteModeType = ReflectionHelper.FindType("FactoryAssembly.InfiniteSequenceMode");
		_currentBombField = _factoryFiniteModeType.GetField("_currentBomb", BindingFlags.NonPublic | BindingFlags.Instance);

		_gameModeProperty = _factoryType.GetProperty("GameMode", BindingFlags.NonPublic | BindingFlags.Instance);

		return _factoryType;
	}

	public static bool TrySetupFactory(Object[] factoryObject, out GameRoom room)
	{
		if (factoryObject == null || factoryObject.Length == 0)
		{
			room = null;
			return false;
		}

		room = new Factory(factoryObject[0]);
		return true;
	}

	private Factory(Object roomObject)
	{
		DebugHelper.Log("Found gameplay room of type Factory Room");
		_gameroom = _gameModeProperty.GetValue(roomObject, new object[] { });
		if (_gameroom.GetType() == _factoryStaticModeType) return;

		_infiniteMode = _gameroom.GetType() == _factoryInfiniteModeType;
		_finiteMode = _gameroom.GetType() == _factoryFiniteModeType;
		BombID = -1;
		HoldBomb = false;
	}

	private Object GetBomb => _finiteMode || _infiniteMode ? (Object) _currentBombField.GetValue(_gameroom) : null;

	public override void InitializeBombs(List<Bomb> bombs)
	{
		if (_gameroom.GetType() == _factoryStaticModeType)
		{
			base.InitializeBombs(bombs);
			return;
		}

		ReuseBombCommander = true;
		BombMessageResponder.Instance.SetBomb(bombs[0], -1);
		BombMessageResponder.Instance.InitializeModuleCodes();
		BombCount = bombs.Count;
	}

	public IEnumerator DestroyBomb(Object bomb)
	{
		yield return new WaitUntil(() => _infiniteMode || bomb == null || _internalBombProperty.GetValue(bomb, null) == null || (bool) _bombEndedProperty.GetValue(bomb, null));
		yield return new WaitForSeconds(0.1f);
		if (_infiniteMode || bomb == null || _internalBombProperty.GetValue(bomb, null) == null) yield break;
		_destroyBombMethod.Invoke(_gameroom, new object[] { bomb });
	}

	public override IEnumerator ReportBombStatus()
	{
		if (_gameroom.GetType() == _factoryStaticModeType)
		{
			IEnumerator baseIEnumerator = base.ReportBombStatus();
			while (baseIEnumerator.MoveNext()) yield return baseIEnumerator.Current;
			yield break;
		}
		InitializeOnLightsOn = false;

		TwitchBombHandle bombHandle = BombMessageResponder.Instance.BombHandles[0];

		bombHandle.BombName = _infiniteMode ? "Infinite bombs incoming" : $"{BombCount} bombs incoming";

		yield return new WaitUntil(() => GetBomb != null || bombHandle.BombCommander.Bomb.HasDetonated);
		if (bombHandle.BombCommander.Bomb.HasDetonated && !_zenMode) yield break;

		float currentBombTimer = bombHandle.BombCommander.TimerComponent.TimeRemaining + 5;
		int currentBombID = 1;
		while (GetBomb != null)
		{
			Object currentBomb = GetBomb;

			TimerComponent timerComponent = bombHandle.BombCommander.TimerComponent;
			yield return new WaitUntil(() => timerComponent.IsActive);

			if (Math.Abs(currentBombTimer - bombHandle.BombCommander.TimerComponent.TimeRemaining) > 1f)
			{
				yield return null;
				InitializeGameModes(true);
			}

			bool enableCameraWall = OtherModes.ZenModeOn && IRCConnection.Instance.State == IRCConnectionState.Connected && TwitchPlaySettings.data.EnableFactoryZenModeCameraWall;
			if (enableCameraWall != BombMessageResponder.ModuleCameras.CameraWallEnabled)
			{
				if (enableCameraWall)
					BombMessageResponder.ModuleCameras.EnableCameraWall();
				else
					BombMessageResponder.ModuleCameras.DisableCameraWall();
			}
			bombHandle.BombName = $"Bomb {currentBombID} of {(_infiniteMode ? "∞" : BombCount.ToString())}";
			IRCConnection.SendMessage("Bomb {0} of {1} is now live.", currentBombID++, _infiniteMode ? "∞" : BombCount.ToString());

			if (TwitchPlaySettings.data.EnableAutomaticEdgework)
			{
				bombHandle.BombCommander.FillEdgework();
			}
			else
			{
				bombHandle.EdgeworkText.text = TwitchPlaySettings.data.BlankBombEdgework;
			}
			if (OtherModes.ZenModeOn)
				bombHandle.BombCommander.StrikeLimit += bombHandle.BombCommander.StrikeCount;

			IEnumerator bombHold = bombHandle.OnMessageReceived(new Message("Bomb Factory", "red", "bomb hold"));
			while (bombHold.MoveNext())
			{
				yield return bombHold.Current;
			}

			Bomb bomb1 = (Bomb) _internalBombProperty.GetValue(currentBomb, null);
			yield return new WaitUntil(() =>
			{
				bool result = bomb1.HasDetonated || bomb1.IsSolved() || !BombMessageResponder.BombActive;
				if (!result || OtherModes.TimeModeOn) currentBombTimer = bomb1.GetTimer().TimeRemaining;
				return result;
			});
			if (!BombMessageResponder.BombActive) yield break;

			IRCConnection.SendMessage(BombMessageResponder.Instance.GetBombResult(false));
			TwitchPlaySettings.SetRetryReward();

			foreach (TwitchModule handle in BombMessageResponder.Instance.ComponentHandles)
			{
				//If the camera is still attached to the bomb component when the bomb gets destroyed, then THAT camera is destroyed as wel.
				BombMessageResponder.ModuleCameras.UnviewModule(handle);
			}

			if (TwitchPlaySettings.data.EnableFactoryAutomaticNextBomb)
			{
				bombHold = bombHandle.OnMessageReceived(new Message("Bomb Factory", "red", "bomb drop"));
				while (bombHold.MoveNext()) yield return bombHold.Current;
			}

			while (currentBomb == GetBomb)
			{
				yield return new WaitForSeconds(0.10f);
				if (currentBomb != GetBomb || !TwitchPlaySettings.data.EnableFactoryAutomaticNextBomb)
					continue;

				bombHold = bombHandle.OnMessageReceived(new Message("Bomb Factory", "red", "bomb hold"));
				while (bombHold.MoveNext()) yield return bombHold.Current;
				yield return new WaitForSeconds(0.10f);

				bombHold = bombHandle.OnMessageReceived(new Message("Bomb Factory", "red", "bomb drop"));
				while (bombHold.MoveNext()) yield return bombHold.Current;
			}

			bombHandle.StartCoroutine(DestroyBomb(currentBomb));

			if (GetBomb == null) continue;
			Bomb bomb = (Bomb) _internalBombProperty.GetValue(GetBomb, null);
			InitializeBomb(bomb);
		}
	}

	private static Type _factoryBombType;
	private static PropertyInfo _internalBombProperty;
	private static PropertyInfo _bombEndedProperty;

	private static Type _factoryType;
	private static Type _factoryModeType;
	private static MethodInfo _destroyBombMethod;

	private static Type _factoryStaticModeType;
	private static Type _factoryFiniteModeType;
	private static Type _factoryInfiniteModeType;

	private static PropertyInfo _gameModeProperty;
	private static FieldInfo _currentBombField;

	private readonly object _gameroom;
}
