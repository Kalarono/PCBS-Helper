using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using cakeslice;

namespace PCBSCalculatorHelper
{
    internal sealed class CableColourReplacement
    {
        private readonly WorkbenchAutomation owner;
        private static readonly FieldInfo Description = AccessTools.Field(typeof(CableMesh), "m_desc");
        private static readonly FieldInfo StartRigidity = AccessTools.Field(typeof(CableMesh), "m_startRigidity");
        private static readonly FieldInfo EndRigidity = AccessTools.Field(typeof(CableMesh), "m_endRigidity");
        private static readonly FieldInfo SubTransforms = AccessTools.Field(typeof(CableMesh), "m_subTransforms");
        private static readonly FieldInfo MeshFilterField = AccessTools.Field(typeof(CableMesh), "m_meshFilter");
        private static readonly FieldInfo MeshField = AccessTools.Field(typeof(CableMesh), "m_mesh");
        private static readonly FieldInfo[] MeshFields = typeof(CableMesh).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        internal CableColourReplacement(WorkbenchAutomation owner) { this.owner = owner; }

        internal void Apply(WorkingOnComputerState state, PartDesc selected)
        {
            Case pc = WorkbenchAutomation.WorkCase.GetValue(state) as Case;
            if (pc == null)
            {
                owner.LogCableReplacement("No workbench PC for selected style " + selected.m_id);
                owner.Notify("No PC available for cable replacement.");
                return;
            }
            CableVisuals visuals = PartsDatabase.GetCableVisuals(selected);
            if (visuals == null) throw new InvalidOperationException("Cable style has no visuals: " + selected.m_id);
            int changed = 0, failed = 0, already = 0, fixedStyle = 0, notReady = 0;
            CableInstance firstChanged = null;
            foreach (CableInstance cable in pc.currentCables.ToArray())
            {
                if (cable == null || cable.Uninstalling || cable.m_start == null || cable.m_end == null ||
                    cable.m_cableMesh == null) { notReady++; continue; }
                CableDesc desc = Description.GetValue(cable.m_cableMesh) as CableDesc;
                if (desc == null || !WorkbenchRules.CanRecolour(desc.m_alwaysUseDefaulVisuals,
                    desc.m_nonInteractive, cable.m_start.m_overrideCableVisuals)) { fixedStyle++; continue; }
                if (cable.m_desc != null && cable.m_desc.m_id == selected.m_id) { already++; continue; }
                try
                {
                    // Retain CableInstance, connectors, plugs and root renderer.
                    // Native Start must never bind to plugs scheduled for destruction.
                    RebuildVisuals(cable.m_cableMesh, desc, visuals);
                    cable.m_desc = selected;
                    cable.m_playerChoice = true;
                    if (firstChanged == null) firstChanged = cable;
                    changed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    owner.Warn("Cable recolour " + cable.m_start.id + " -> " + cable.m_end.id, ex);
                }
            }
            if (changed > 0)
            {
                pc.OnChangePart(firstChanged.gameObject);
                pc.UpdateGameStatus();
            }
            owner.LogCableReplacement("Style=" + selected.m_id + ", changed=" + changed + ", failed=" + failed +
                ", already=" + already + ", fixed=" + fixedStyle + ", notReady=" + notReady);
            owner.Notify(WorkbenchRules.CableReplacementMessage(changed, failed, already));
        }

        private static void RebuildVisuals(CableMesh current, CableDesc desc, CableVisuals visuals)
        {
            MeshRenderer renderer = current.GetComponent<MeshRenderer>();
            MeshFilter filter = current.GetComponent<MeshFilter>();
            Transform[] oldSegments = (Transform[])SubTransforms.GetValue(current);
            CableInstance cable = current.GetComponent<CableInstance>();
            if (renderer == null || filter == null || oldSegments == null || cable == null)
                throw new InvalidOperationException("Cable mesh is not initialized.");
            object[] oldFields = MeshFields.Select(x => x.GetValue(current)).ToArray();
            Material[] oldMaterials = renderer.sharedMaterials;
            Mesh oldMesh = filter.sharedMesh;
            int oldLayer = current.gameObject.layer;
            GameObject staging = new GameObject("PCBS Helper Cable Style Staging");
            staging.transform.SetParent(current.transform.parent, false);
            staging.transform.localPosition = current.transform.localPosition;
            staging.transform.localRotation = current.transform.localRotation;
            staging.transform.localScale = current.transform.localScale;
            CableMesh prepared = staging.AddComponent<CableMesh>();
            Transform[] newSegments = null;
            bool committed = false;
            try
            {
                prepared.Init(desc, visuals, current.m_transforms, cable.m_start.hasEndConnector,
                    (float)StartRigidity.GetValue(current) / 0.2f, (float)EndRigidity.GetValue(current) / 0.2f);
                newSegments = (Transform[])SubTransforms.GetValue(prepared);
                MeshRenderer preparedRenderer = prepared.GetComponent<MeshRenderer>();
                // Preserve native collision visibility through inventory transitions.
                bool enabled = current.m_boxColliders == null || current.m_boxColliders.Length == 0 || current.m_boxColliders[0].enabled;
                foreach (BoxCollider collider in prepared.m_boxColliders) collider.enabled = enabled;
                foreach (Transform segment in newSegments) segment.SetParent(current.transform, true);
                for (int i = 0; i < MeshFields.Length; i++) MeshFields[i].SetValue(current, MeshFields[i].GetValue(prepared));
                MeshFilterField.SetValue(current, filter);
                renderer.sharedMaterials = preparedRenderer.sharedMaterials;
                filter.sharedMesh = (Mesh)MeshField.GetValue(prepared);
                current.gameObject.layer = staging.layer;
                CustomOutline outline = current.GetComponent<CustomOutline>();
                if (outline != null) outline.originalMaterials = renderer.sharedMaterials;
                // Finish native raycast geometry before the next input frame.
                current.RegenerateMesh();
                committed = true;
                foreach (Transform segment in oldSegments) if (segment != null) UnityEngine.Object.DestroyImmediate(segment.gameObject);
                if (oldMesh != null) UnityEngine.Object.Destroy(oldMesh);
            }
            finally
            {
                if (!committed)
                {
                    for (int i = 0; i < MeshFields.Length; i++) MeshFields[i].SetValue(current, oldFields[i]);
                    renderer.sharedMaterials = oldMaterials; filter.sharedMesh = oldMesh; current.gameObject.layer = oldLayer;
                    CustomOutline outline = current.GetComponent<CustomOutline>();
                    if (outline != null) outline.originalMaterials = oldMaterials;
                    if (newSegments != null) foreach (Transform segment in newSegments)
                        if (segment != null && segment.parent == current.transform) UnityEngine.Object.DestroyImmediate(segment.gameObject);
                    Mesh failedMesh = (Mesh)MeshField.GetValue(prepared);
                    if (failedMesh != null) UnityEngine.Object.Destroy(failedMesh);
                }
                UnityEngine.Object.DestroyImmediate(staging);
            }
        }
    }
}
