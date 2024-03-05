﻿using System;
using BepInEx;
using UnityEngine;
using SlugBase.Features;
using static SlugBase.Features.FeatureTypes;
using System.Security.Permissions;
using System.Runtime.CompilerServices;
using RWCustom;

namespace FCAP
{
    [BepInPlugin("alduris.fcap", "Five Cycles at Pebbles", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        public static SlugcatStats.Name Nightguard;

        // Add hooks
        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            On.RoomSpecificScript.AddRoomSpecificScript += RoomSpecificScript_AddRoomSpecificScript;
            On.Player.checkInput += Player_checkInput;
        }

        private void Player_checkInput(On.Player.orig_checkInput orig, Player self)
        {
            if (self.playerState.playerNumber == 0 && self.SlugCatClass == Nightguard && GameController.Instance != null)
            {
                var game = GameController.Instance;
                for (int i = self.input.Length - 1; i > 0; i--)
                {
                    self.input[i] = self.input[i - 1];
                }

                var currInput = RWInput.PlayerInput(0, Custom.rainWorld);
                var lastInput = CWTs.LastInput(self);
                var controls = self.room.game.rainWorld.options.controls[0];
                if (game.InCams)
                {
                    // In cams mode, player cannot move
                    self.input[0] = new Player.InputPackage(controls.gamePad, controls.GetActivePreset(), 0, 0, false, false, false, false, false);

                    // Stop using cams if grab
                    if (currInput.pckp && !lastInput.pckp)
                    {
                        game.ToggleCams();
                    }

                    // Switch cams
                    if (currInput.jmp && !lastInput.jmp)
                        game.SwitchCamViewing();
                    // Or switch cam selection
                    else if (currInput.x > 0 && lastInput.x <= 0 && currInput.y == 0)
                        game.SwitchCamSelecting(Map.Direction.Right);
                    else if (currInput.x < 0 && lastInput.x >= 0 && currInput.y == 0)
                        game.SwitchCamSelecting(Map.Direction.Left);
                    else if (currInput.y > 0 && lastInput.y <= 0 && currInput.x == 0)
                        game.SwitchCamSelecting(Map.Direction.Up);
                    else if (currInput.y < 0 && lastInput.y >= 0 && currInput.x == 0)
                        game.SwitchCamSelecting(Map.Direction.Down);
                }
                else
                {
                    // In not cams mode, player can move but cannot throw, pick up, or use map
                    self.input[0] = new Player.InputPackage(controls.gamePad, controls.GetActivePreset(), currInput.x, currInput.y, currInput.jmp, false, false, false, currInput.crouchToggle);

                    // Toggle door or cams
                    if (currInput.thrw && !lastInput.thrw && !game.OutOfPower)
                    {
                        float x = self.bodyChunks[0].pos.x / self.room.PixelWidth;
                        switch (x)
                        {
                            case < 0.4f:
                                {
                                    game.ToggleDoor(Map.Direction.Left);
                                    break;
                                }
                            case < 0.6f:
                                {
                                    game.ToggleCams();
                                    break;
                                }
                            default:
                                {
                                    game.ToggleDoor(Map.Direction.Right);
                                    break;
                                }
                        }
                    }
                }
                CWTs.UpdateLastInput(self, currInput);
            }
            else
            {
                orig(self);
            }
        }

        private void RoomSpecificScript_AddRoomSpecificScript(On.RoomSpecificScript.orig_AddRoomSpecificScript orig, Room room)
        {
            orig(room);

            if (room.abstractRoom.name == "SS_FCAP")
            {
                room.AddObject(new GameController(room));
            }
        }

        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
            Nightguard = new SlugcatStats.Name("Nightguard", false);
        }
    }
}