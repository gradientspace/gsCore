// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using g3;
using f3;

namespace gs
{
    // shows a popup radial menu at right-click location
    public class SceneRightClickBehavior : StandardInputBehavior
    {
        Cockpit cockpit;
        HUDRadialMenu popup;

        public enum PlacementMode
        {
            InScene,
            OnHUDSphere
        }
        PlacementMode Placement { get; set; }
        public float VisualDiameter { get; set; }
        
        // [0..1] multiplier of VisualDiameter used to set text size
        public float TextScale { get; set; }

        // for OnHUDSphere mode
        public float HUDRadius { get; set; }


        public SceneRightClickBehavior(Cockpit cockpit)
        {
            this.cockpit = cockpit;
            Priority = 20;
            HUDRadius = 1.0f;
            VisualDiameter = 25.0f;
            TextScale = 0.018f;
            Placement = PlacementMode.InScene;
        }


        public override InputDevice SupportedDevices
            { get { return InputDevice.Gamepad | InputDevice.Mouse | InputDevice.AnySpatialDevice; } }


        public HUDRadialMenu GenerateDefaultRadialMenu(float fDiameter, AnyRayHit rayHit)
        {
            HUDRadialMenu popup = new HUDRadialMenu();
            popup.Radius = fDiameter * 0.5f;
            popup.TextScale = fDiameter * TextScale;

            popup.SubItemRadialWidth = popup.Radius * 0.75f;
            popup.SubItemRadialPadding = popup.Radius * 0.05f;
            popup.SubItemTextScale = popup.TextScale * 0.75f;

            float Pullback = 2.0f;

            HUDRadialMenu.MenuItem teleport = popup.AppendMenuItem("Teleport", (s, o) => {
                Teleporter.TeleportTowards(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, Pullback);
            });
            popup.AppendSubMenuItem(teleport, "Teleport\nLevel", (s, o) => {
                Teleporter.TeleportTowards_Level(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, Pullback);
            });
            popup.AppendSubMenuItem(teleport, "Teleport\nNormal", (s, o) => {
                Teleporter.Teleport_Normal_Auto(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, rayHit.hitNormal, Pullback);
            });
            popup.AppendMenuItem("Center", (s, o) => {
                cockpit.ActiveCamera.Animator().AnimatePanFocus(rayHit.hitPos, CoordSpace.WorldCoords, 0.3f);
            });

            return popup;
        }


        public HUDRadialMenu GenerateSceneObjectRadialMenu(float fDiameter, AnyRayHit rayHit)
        {
            HUDRadialMenu popup = new HUDRadialMenu();
            popup.Radius = fDiameter * 0.5f;
            popup.TextScale = fDiameter * TextScale;

            popup.SubItemRadialWidth = popup.Radius * 0.75f;
            popup.SubItemRadialPadding = popup.Radius * 0.05f;
            popup.SubItemTextScale = popup.TextScale * 0.75f;

            float Pullback = 2.0f;
            HUDRadialMenu.MenuItem teleport = popup.AppendMenuItem("Teleport", (s, o) => {
                Teleporter.TeleportTowards(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, Pullback);
            });
            popup.AppendSubMenuItem(teleport, "Teleport\nLevel", (s, o) => {
                Teleporter.TeleportTowards_Level(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, Pullback);
            });
            popup.AppendSubMenuItem(teleport, "Teleport\nNormal", (s, o) => {
                Teleporter.Teleport_Normal_Auto(cockpit.ActiveCamera, cockpit.Scene, rayHit.hitPos, rayHit.hitNormal, Pullback);
            });

            popup.AppendMenuItem("Center", (s, o) => {
                cockpit.ActiveCamera.Animator().AnimatePanFocus(rayHit.hitPos, CoordSpace.WorldCoords, 0.3f);
            });
            if (rayHit.hitSO != null) {
                popup.AppendMenuItem("Delete", (s, o) => {
                    cockpit.Scene.History.PushChange(
                        new DeleteSOChange() { scene = cockpit.Scene, so = rayHit.hitSO });
                    cockpit.Scene.History.PushInteractionCheckpoint();
                });
            }

            // Ungroup menu item
            if ( cockpit.Scene.Selected.Count == 0 && rayHit.hitSO is GroupSO) {
                popup.AppendMenuItem("Ungroup", (s, o) => {
                    cockpit.Scene.History.PushChange(
                        new UnGroupChange() { Group = rayHit.hitSO as GroupSO, Scene = cockpit.Scene }, false);
                    cockpit.Scene.History.PushInteractionCheckpoint();
                });                
            }

            // Group menu item
            int nGroupable = cockpit.Scene.Selected.Count;
            if ( nGroupable > 1 ) {
                popup.AppendMenuItem("Group", (s, o) => {
                    List<SceneObject> Selected = new List<SceneObject>();
                    foreach ( var so in cockpit.Scene.Selected ) {
                        Selected.Add(so);
                    }
                    // clear selection and then do this command next frame, so that any active gizmos can clean themselves up!
                    cockpit.Scene.ClearSelection();
                    cockpit.Context.RegisterNextFrameAction(() => {
                        cockpit.Scene.History.PushChange(
                            new CreateGroupChange() { Objects = Selected, Scene = cockpit.Scene }, false);
                        cockpit.Scene.History.PushInteractionCheckpoint();
                    });
                });
            }

            return popup;
        }

        public HUDRadialMenu GeneratePivotRadialMenu(float fDiameter, PivotSO pivot)
        {
            HUDRadialMenu popup = new HUDRadialMenu();
            popup.Radius = fDiameter * 0.5f;
            popup.TextScale = fDiameter * TextScale;
            float Pullback = 2.0f;

            if (cockpit.Context.TransformManager.HaveActiveGizmo &&
                cockpit.Context.TransformManager.ActiveGizmo.SupportsReferenceObject ) 
            {
                popup.AppendMenuItem("Use\nFrame", (s, o) => {
                    cockpit.Context.TransformManager.SetActiveReferenceObject(pivot);
                });
            }
            popup.AppendMenuItem("Teleport\nHere", (s, o) => {
                Teleporter.TeleportTowards_Level(cockpit.ActiveCamera, cockpit.Scene, 
                    pivot.GetLocalFrame(CoordSpace.WorldCoords).Origin, Pullback);
            });
            popup.AppendMenuItem("Center", (s, o) => {
                cockpit.ActiveCamera.Animator().AnimatePanFocus(
                    pivot.GetLocalFrame(CoordSpace.WorldCoords).Origin, CoordSpace.WorldCoords, 0.3f);
            });
            popup.AppendMenuItem("Delete", (s, o) => {
                cockpit.Scene.History.PushChange(
                    new DeleteSOChange() { scene = cockpit.Scene, so = pivot });
                cockpit.Scene.History.PushInteractionCheckpoint();
            });

            return popup;
        }



        public override CaptureRequest WantsCapture(InputState input)
        {
            // don't show if anyone else is capturing (how did we even get here??)
            // [TODO] should improve this in the future. There are places where we would want to
            //   show, but we would have to restrict menu items (eg moving camera while in
            //   active xform breaks things).
            if (input.LeftCaptureActive || input.RightCaptureActive)
                return CaptureRequest.Ignore;

            // currently only supporting a single popup at a time!
            if (popup != null)
                return CaptureRequest.Ignore;

            bool bActivate = false;
            CaptureSide eSide = CaptureSide.Any;
            Ray3f useRay = new Ray3f(Vector3f.Zero, Vector3f.AxisY);
            if (input.IsForDevice(InputDevice.Mouse) || input.IsForDevice(InputDevice.Gamepad)) {
                bActivate = input.bRightMousePressed || input.bRightTriggerPressed;
                useRay = (input.bRightMousePressed) ? input.vMouseWorldRay : input.vGamepadWorldRay;
            } else if (input.IsForDevice(InputDevice.AnySpatialDevice)) {
                bActivate = input.bXButtonPressed || input.bAButtonPressed;
                useRay = (input.bXButtonPressed) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
                eSide = (input.bXButtonPressed) ? CaptureSide.Left : CaptureSide.Right;
            }
            if (!bActivate)
                return CaptureRequest.Ignore;

            AnyRayHit rayHit;
            if (cockpit.Scene.FindSceneRayIntersection(useRay, out rayHit) == false)
                return CaptureRequest.Ignore;

            return CaptureRequest.Begin(this, eSide);
        }


        public override Capture BeginCapture(InputState input, CaptureSide eSide)
        {
            if (input.LeftCaptureActive || input.RightCaptureActive || popup != null) {
                DebugUtil.Warning("SceneRightClickBehavior.BeginCapture - we should not be here...");
                return Capture.Ignore;
            }

            Ray3f useRay = new Ray3f(Vector3f.Zero, Vector3f.AxisY);
            if ( input.IsForDevice(InputDevice.Mouse) || input.IsForDevice(InputDevice.Gamepad) ) {
                useRay = (input.bRightMousePressed) ? input.vMouseWorldRay : input.vGamepadWorldRay;
            } else if ( input.IsForDevice(InputDevice.AnySpatialDevice) ) {
                useRay = (eSide == CaptureSide.Left) ? input.vLeftSpatialWorldRay : input.vRightSpatialWorldRay;
            }

            // raycast into scene to find hit object/position for menu. We try Pivots first,
            // because they are special and have priority (?? always ??). Then we cast into general scene.
            SORayHit pivotHit;
            bool bHitPivot = cockpit.Scene.FindSORayIntersection(useRay, out pivotHit, (x) => { return (x is PivotSO); });
            AnyRayHit rayHit;
            if (bHitPivot) {
                rayHit = new AnyRayHit(pivotHit);
            } else {
                if (cockpit.Scene.FindSceneRayIntersection(useRay, out rayHit) == false)
                    return Capture.Ignore;
            }

            // find center of menu in space
            Vector3f vHUDCenter = cockpit.RootGameObject.GetPosition();
            Vector3f menuLocation = rayHit.hitPos;
            if (Placement == PlacementMode.OnHUDSphere) {
                float fRayT;
                bool bHit = RayIntersection.Sphere(useRay.Origin, useRay.Direction,
                    vHUDCenter, HUDRadius, out fRayT);
                Debug.Assert(bHit);
                menuLocation = useRay.Origin + fRayT * useRay.Direction;
            }

            // compute extents
            float fDiameter = VRUtil.GetVRRadiusForVisualAngle(
                menuLocation, cockpit.ActiveCamera.GetPosition(), VisualDiameter);

            if (rayHit.eType == HitType.SceneObjectHit && rayHit.hitSO is PivotSO)
                popup = GeneratePivotRadialMenu(fDiameter, rayHit.hitSO as PivotSO);
            else if (rayHit.eType == HitType.SceneObjectHit)
                popup = GenerateSceneObjectRadialMenu(fDiameter, rayHit);
            else
                popup = GenerateDefaultRadialMenu(fDiameter, rayHit);
            popup.Create();
            popup.Name = "popup_menu";

            if ( Placement == PlacementMode.InScene ) {
                HUDUtil.PlaceInScene(popup, vHUDCenter, menuLocation);
            } else {
                HUDUtil.PlaceInSphere(popup, HUDRadius, vHUDCenter, menuLocation);
            }

            // this is a bit of a hack...radial menu lives in-scene, if we attach it to
            //   cockpit then it will move with cockpit, which is wrong. So we want to
            //   stick it in the scene. But, at least for now, we still want it to be
            //   drawn in the cockpit layer, so we add to cockpit first, them remove and
            //   re-add to the scene
            cockpit.AddUIElement(popup, false);
            cockpit.RemoveUIElement(popup, false);
            cockpit.Scene.AddUIElement(popup, false);

            HUDUtil.AnimatedShow(popup, 0.2f);

            return Capture.Begin(this, eSide);
        }


        public override Capture UpdateCapture(InputState input, CaptureData data)
        {
            bool bReleased = false;
            bool bContinue = false;
            InputEvent e = null;
            if (input.IsForDevice(InputDevice.Mouse) ) {
                bReleased = input.bRightMouseReleased;
                bContinue = input.bRightMouseDown;
                e = InputEvent.Mouse(input);
            } else if (input.IsForDevice(InputDevice.Gamepad)) {
                bReleased = input.bRightTriggerReleased;
                bContinue = input.bRightTriggerDown;
                e = InputEvent.Gamepad(input);
            } else if (input.IsForDevice(InputDevice.AnySpatialDevice)) {
                bReleased =
                    (data.which == CaptureSide.Left && input.bXButtonReleased) ||
                    (data.which == CaptureSide.Right && input.bAButtonReleased);
                bContinue =
                    (data.which == CaptureSide.Left && input.bXButtonDown) ||
                    (data.which == CaptureSide.Right && input.bAButtonDown);
                e = InputEvent.Spatial(data.which, input);
            }


            if (bReleased) {
                UIRayHit hit = new UIRayHit();
                if ( popup.FindRayIntersection(e.ray, out hit) ) {
                    popup.EndCapture(e);
                }

                // do animated dismiss that will destroy menu on completion
                HUDUtil.AnimatedDimiss_Scene(popup, cockpit.Scene, true, 0.2f);

                // lose our reference
                popup = null;

                return Capture.End;
            } else if (bContinue) {
                popup.UpdateCapture(e);
                return Capture.Continue;
            } 
            // should never get here...
            DebugUtil.Log(2, "[RightClickBehavior::UpdateCapture] how did we get here?");
            return Capture.End;
        }


        public override Capture ForceEndCapture(InputState input, CaptureData data)
        {
            // do animated dismiss that will destroy menu on completion
            HUDUtil.AnimatedDimiss_Scene(popup, cockpit.Scene, true, 0.2f);
            // lose our reference
            popup = null;
            return Capture.End;
        }

    }
}
