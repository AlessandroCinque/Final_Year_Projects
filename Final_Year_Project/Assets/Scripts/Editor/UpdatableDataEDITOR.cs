using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
// the last "true" is for make it work for the child classes
[CustomEditor(typeof(UpdatableData),true)]
public class UpdatableDataEDITOR : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        UpdatableData data = (UpdatableData)target;
        if (GUILayout.Button("Update"))
        {
            data.NotifyOfUpdatedValues();
            EditorUtility.SetDirty(target);

        }
    }
}
