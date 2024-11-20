using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[Serializable]
public class Decal
{
    public string name;
    public Vector3 scale;
    public Texture2D albedoTex;
    public Texture2D normalTex;
    public Texture2D maskTex;
    public Texture2D alphaTex;
}

public class OutdoorsSceneMgmt : MonoBehaviour
{
    public InputActionReference screenPositionIAR;
    public InputActionReference paintIAR;
    public List<Decal> decalList;

    Decal decal;
    Vector2 screenPosition;

    void Start()
    {
        var screenPositionIA = screenPositionIAR.action;
        var paintIA = paintIAR.action;

        screenPositionIA.performed += (cx) => {
            screenPosition = cx.ReadValue<Vector2>();
        };

        paintIA.performed += (cx) => {
            if (decal == null) return;

            var ray = Camera.main.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var rayHit))
            {
                var tangentDecals = FindObjectsByType<TangentDecal>(FindObjectsSortMode.None);
                foreach (var tangentDecal in tangentDecals)
                {
                    tangentDecal.Paint(
                        decal.albedoTex,
                        decal.normalTex,
                        decal.maskTex,
                        decal.alphaTex,
                        rayHit.point,
                        rayHit.normal,
                        UnityEngine.Random.insideUnitSphere,
                        decal.scale
                    );
                }
            }
        };

        screenPositionIA.Enable();
        paintIA.Enable();
    }

    void OnGUI()
    {
        for (var i = 0; i < decalList.Count; i++)
        {
            var name = decalList[i].name;

            if (GUI.Button(new Rect(10, 10 + 20 * i, 120, 20), name))
            {
                decal = decalList[i];
            }
        }
    }
}
