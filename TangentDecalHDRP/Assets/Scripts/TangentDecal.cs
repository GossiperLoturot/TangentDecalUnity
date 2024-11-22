using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
public class ItemDescriptor
{
    public int width;
    public int height;
    public Material material;
    public string albedoTexName;
    public string normalTexName;
    public string maskTexName;
    public string alphaTexName;
    public List<Renderer> renderers;
}

public class Item
{
    public RTHandle albedoRT;
    public RTHandle normalRT;
    public RTHandle maskRT;
    public RTHandle alphaRT;
    public Material material;
    public List<Renderer> renderers;
}

class PaintQuery
{
    public RTHandle albedoRT;
    public RTHandle normalRT;
    public RTHandle maskRT;
    public RTHandle alphaRT;
    public Material material;
    public List<Renderer> renderers;
    public Texture2D albedoTex;
    public Texture2D normalTex;
    public Texture2D maskTex;
    public Texture2D alphaTex;
    public Vector3 position;
    public Vector3 normal;
    public Vector3 tangent;
    public Vector3 scale;
}

class Drawcall
{
    public RTHandle mainRT;
    public Texture decalTex;
    public int shaderPass;
}

public class TangentDecal : MonoBehaviour
{
    public List<ItemDescriptor> itemDescriptors = new List<ItemDescriptor>();

    CustomPassVolume volume;
    TangentDecalPass pass;

    void OnEnable()
    {
        volume = gameObject.AddComponent<CustomPassVolume>();
        volume.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;

        pass = volume.AddPassOfType<TangentDecalPass>();
        pass.Init(itemDescriptors);
    }

    void OnDisable()
    {
        Destroy(volume);
    }

    public void Paint(Texture2D albedoTex, Texture2D normalTex, Texture2D maskTex, Texture2D alphaTex, Vector3 position, Vector3 normal, Vector3 tangent, Vector3 scale)
    {
        pass.Paint(albedoTex, normalTex, maskTex, alphaTex, position, normal, tangent, scale);
    }
}

class TangentDecalPass : CustomPass
{
    Shader shader;
    List<Item> items = new List<Item>();
    List<PaintQuery> queue = new List<PaintQuery>();

    public void Init(List<ItemDescriptor> itemDescriptors)
    {
        Cleanup();

        foreach (var itemDescriptor in itemDescriptors)
        {
            var albedoRT = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.albedoTexName, albedoRT);

            var normalRT = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.normalTexName, normalRT);

            var maskRT = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.maskTexName, maskRT);

            var alphaRT = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.alphaTexName, alphaRT);

            var item = new Item()
            {
                albedoRT = albedoRT,
                normalRT = normalRT,
                maskRT = maskRT,
                alphaRT = alphaRT,
                material = itemDescriptor.material,
                renderers = itemDescriptor.renderers
            };
            items.Add(item);
        }
    }

    public void Paint(Texture2D albedoTex, Texture2D normalTex, Texture2D maskTex, Texture2D alphaTex, Vector3 position, Vector3 normal, Vector3 tangent, Vector3 scale)
    {
        foreach (var item in items)
        {
            var query = new PaintQuery()
            {
                albedoRT = item.albedoRT,
                normalRT = item.normalRT,
                maskRT = item.maskRT,
                alphaRT = item.alphaRT,
                renderers = item.renderers,
                material = item.material,
                albedoTex = albedoTex,
                normalTex = normalTex,
                maskTex = maskTex,
                alphaTex = alphaTex,
                position = position,
                normal = normal,
                tangent = tangent,
                scale = scale
            };
            queue.Add(query);
        }
    }

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        shader = Shader.Find("Hidden/TangentDecal");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        foreach (var query in queue)
        {
            // Register Albedo and Normal, Mask drawcall if available
            var drawcalls = new Drawcall[]
            {
                new Drawcall() { mainRT = query.albedoRT, decalTex = query.albedoTex, shaderPass = 0 },
                new Drawcall() { mainRT = query.normalRT, decalTex = query.normalTex, shaderPass = 1 },
                new Drawcall() { mainRT = query.maskRT, decalTex = query.maskTex, shaderPass = 2 },
                new Drawcall() { mainRT = query.alphaRT, decalTex = query.alphaTex, shaderPass = 3 }
            };

            // Draw decal on texture
            foreach (var drawcall in drawcalls)
            {
                var bufferRT = RTHandles.Alloc(drawcall.mainRT.rt.width, drawcall.mainRT.rt.height);

                // Create new material for buffering render resource
                var material = CoreUtils.CreateEngineMaterial(shader);
                material.SetTexture("_MainTex", drawcall.mainRT);
                material.SetTexture("_DecalTex", drawcall.decalTex);
                material.SetVector("_DecalPosition", query.position);
                material.SetVector("_DecalNormal", query.normal);
                material.SetVector("_DecalTangent", query.tangent);
                material.SetVector("_DecalScale", query.scale);

                CoreUtils.SetRenderTarget(ctx.cmd, bufferRT, ClearFlag.Color);

                foreach (var renderer in query.renderers)
                {
                    for (var i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        if (renderer.sharedMaterials[i].GetInstanceID() == query.material.GetInstanceID())
                        {
                            ctx.cmd.DrawRenderer(renderer, material, i, drawcall.shaderPass);
                        }
                    }
                }

                ctx.cmd.Blit(bufferRT, drawcall.mainRT);

                RTHandles.Release(bufferRT);
                CoreUtils.Destroy(material);
            }

        }
        queue.Clear();
    }

    protected override void Cleanup()
    {
        foreach (var item in items)
        {
            RTHandles.Release(item.albedoRT);
            RTHandles.Release(item.normalRT);
            RTHandles.Release(item.maskRT);
            RTHandles.Release(item.alphaRT);
        }
    }
}
