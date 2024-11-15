﻿using FCAP.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using OverseerHolograms;
using UnityEngine;
using static FCAP.Constants;

namespace FCAP.Hooks
{
    internal static class OverseerHooks
    {
        public static void Apply()
        {
            IL.OverseerAI.Update += OverseerAI_Update;
            On.OverseerAbstractAI.RoomAllowed += OverseerAllowedInRoom;
            On.OverseerAbstractAI.PlayerGuideUpdate += OverseerStayWithPlayer;
            On.Overseer.Update += Overseer_Update;
            On.Overseer.TryAddHologram += OverseerAddOurHolograms;
            On.OverseerAI.HoverScoreOfTile += OverseerAI_HoverScoreOfTile;
            On.Room.AddObject += AddObjectNullPatch;
        }

        private static void OverseerAI_Update(ILContext il)
        {
            var c = new ILCursor(il);

            // Part 1: when watching or projecting as a FCAP overseer, do not care about other creatures in the room.

            // Go to the relevant position.
            c.GotoNext(x => x.MatchCallOrCallvirt(typeof(OverseerAI).GetMethod(nameof(OverseerAI.UpdateTempHoverPosition))));
            c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(AbstractCreature).GetProperty(nameof(AbstractCreature.realizedCreature)).GetGetMethod()));

            // Then modify the if-statement to (abstractCreature != null && !CWTs.HasTask(this.overseer)) using weird logic
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((AbstractCreature old, OverseerAI self) => CWTs.HasTask(self.overseer) ? null : old);


            // Part 2: when sitting in a wall as an FCAP overseer, you are allowed to come out of the wall if there is a creature nearby.

            // Find the relevant position.
            c.GotoNext(x => x.MatchLdfld<Overseer>(nameof(Overseer.mode)), x => x.MatchLdsfld<Overseer.Mode>(nameof(Overseer.Mode.SittingInWall)));
            int varNum = -1;
            c.GotoNext(x => x.MatchStloc(out varNum)); // Take the relevant variable index while we're at it
            c.GotoNext(x => x.MatchLdsfld<Overseer.Mode>(nameof(Overseer.Mode.Emerging)));
            c.GotoPrev(MoveType.After, x => x.MatchLdloc(varNum));

            // Now make the if condition always true if we're an FCAP overseer; that is, changing it to (!flag || CWTs.HasTask(self.overseer)), once again with weird logic
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate((int old, OverseerAI self) => CWTs.HasTask(self.overseer) ? 0 : old);
        }

        private static bool OverseerAllowedInRoom(On.OverseerAbstractAI.orig_RoomAllowed orig, OverseerAbstractAI self, int room)
        {
            // Overseer only allowed in SS_FCAP in nightguard
            return orig(self, room) && (!self.world.game.IsStorySession || self.world.game.StoryCharacter != Nightguard || self.world.GetAbstractRoom(room).name == "SS_FCAP");
        }

        private static void OverseerStayWithPlayer(On.OverseerAbstractAI.orig_PlayerGuideUpdate orig, OverseerAbstractAI self, int time)
        {
            // Overseer want to stay with player if game is running
            orig(self, time);

            if (GameController.Instance != null)
            {
                if (!GameController.Instance.OutOfPower)
                {
                    self.goToPlayer = true;
                    self.playerGuideCounter = 1000;
                }
            }
        }

        private static void Overseer_Update(On.Overseer.orig_Update orig, Overseer self, bool eu)
        {
            orig(self, eu);
            if (GameController.Instance != null && CWTs.HasTask(self))
            {
                // (self.abstractCreature.abstractAI as OverseerAbstractAI).goToPlayer = true;
                var value = CWTs.GetTask(self);

                switch (value)
                {
                    case Enums.OverseerTask.Cameras:
                        {
                            if (self.hologram == null)
                            {
                                self.TryAddHologram(CamsHolo, null, float.MaxValue);
                            }
                            break;
                        }
                    case Enums.OverseerTask.LeftDoor:
                        {
                            if (self.hologram == null)
                            {
                                self.TryAddHologram(DoorHolo, null, float.MaxValue);
                            }
                            break;
                        }
                    case Enums.OverseerTask.RightDoor:
                        {
                            if (self.hologram == null)
                            {
                                self.TryAddHologram(DoorHolo, null, float.MaxValue);
                            }
                            break;
                        }
                }
            }
        }

        private static void OverseerAddOurHolograms(On.Overseer.orig_TryAddHologram orig, Overseer self, OverseerHologram.Message message, Creature communicateWith, float importance)
        {
            orig(self, message, communicateWith, importance);

            if (self.hologram == null && GameController.Instance != null)
            {
                if (message == CamsHolo)
                {
                    self.hologram = new CamHologram(GameController.Instance, self, message, null, float.MaxValue);
                }
                else if (message == DoorHolo)
                {
                    self.hologram = new DoorHologram(GameController.Instance, self, message, null, float.MaxValue);
                }
                self.room.AddObject(self.hologram);
            }
        }

        private static float OverseerAI_HoverScoreOfTile(On.OverseerAI.orig_HoverScoreOfTile orig, OverseerAI self, RWCustom.IntVector2 testTile)
        {
            // return orig(self, testTile);
            if (GameController.Instance != null && CWTs.HasTask(self.overseer) && CWTs.GetTask(self.overseer) != Enums.OverseerTask.Cameras && self.overseer.hologram != null)
            {
                if (testTile.x < 0 || testTile.y < 0 || testTile.x >= self.overseer.room.TileWidth || testTile.y >= self.overseer.room.TileHeight)
                {
                    return float.MaxValue;
                }
                if (self.overseer.room.GetTile(testTile).Solid)
                {
                    return float.MaxValue;
                }
                if (self.overseer.room.aimap.getTerrainProximity(testTile) > (int)(6f * self.overseer.size))
                {
                    return float.MaxValue;
                }
                return self.overseer.hologram.InfluenceHoverScoreOfTile(testTile, 0f);
            }
            else
            {
                return orig(self, testTile);
            }
        }

        private static void AddObjectNullPatch(On.Room.orig_AddObject orig, Room self, UpdatableAndDeletable obj)
        {
            // Fix for null holograms so I don't have to IL hook Overseer.TryAddHologram :leditoroverload:
            if (obj != null)
            {
                orig(self, obj);
            }
        }
    }
}
