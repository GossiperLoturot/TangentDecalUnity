using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
public class ItemDescriptor
{
    public int width = 1024;
    public int height = 1024;
    public Material material;
    public string albedoTexName = "_DecalAlbedoMap";
    public string normalTexName = "_DecalNormalMap";
    public string maskTexName = "_DecalMaskMap";
    public List<Renderer> renderers;
}

public class Item
{
    public RTHandle albedoBuffer;
    public RTHandle normalBuffer;
    public RTHandle maskBuffer;
    public Material material;
    public List<Renderer> renderers;
}

class PaintQuery
{
    public RTHandle albedoBuffer;
    public RTHandle normalBuffer;
    public RTHandle maskBuffer;
    public Material material;
    public List<Renderer> renderers;
    public Texture2D albedoTex;
    public Texture2D normalTex;
    public Texture2D maskTex;
    public Vector3 position;
    public Vector3 normal;
    public Vector3 tangent;
    public Vector3 scale;
}

class Drawcall
{
    public RTHandle readBuffer;
    public RTHandle buffer;
    public int pass;
}

public class TangentDecal : MonoBehaviour
{
    public int width = 1024;
    public int height = 1024;
    public List<ItemDescriptor> itemDescriptors = new List<ItemDescriptor>();

    CustomPassVolume volume;
    TangentDecalPass pass;

    void OnEnable()
    {
        volume = gameObject.AddComponent<CustomPassVolume>();
        volume.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;

        pass = volume.AddPassOfType<TangentDecalPass>();
        pass.Init(width, height, itemDescriptors);
    }

    void OnDisable()
    {
        if (volume != null) Destroy(volume);
    }

    public void Paint(Texture2D albedoTex, Texture2D normalTex, Texture2D maskTex, Vector3 position, Vector3 normal, Vector3 tangent, Vector3 scale)
    {
        pass.Paint(albedoTex, normalTex, maskTex, position, normal, tangent, scale);
    }
}

class TangentDecalPass : CustomPass
{
    List<Item> items = new List<Item>();
    List<PaintQuery> queue = new List<PaintQuery>();
    Shader shader;
    RTHandle albedoBuffer;
    RTHandle normalBuffer;
    RTHandle maskBuffer;

    public void Init(int width, int height, List<ItemDescriptor> itemDescriptors)
    {
        Cleanup();

        albedoBuffer = RTHandles.Alloc(width, height);
        normalBuffer = RTHandles.Alloc(width, height);
        maskBuffer = RTHandles.Alloc(width, height);

        foreach (var itemDescriptor in itemDescriptors)
        {
            var albedoBuffer = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.albedoTexName, albedoBuffer);

            var normalBuffer = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.normalTexName, normalBuffer);

            var maskBuffer = RTHandles.Alloc(itemDescriptor.width, itemDescriptor.height);
            itemDescriptor.material.SetTexture(itemDescriptor.maskTexName, maskBuffer);

            var item = new Item()
            {
                material = itemDescriptor.material,
                renderers = itemDescriptor.renderers,
                albedoBuffer = albedoBuffer,
                normalBuffer = normalBuffer,
                maskBuffer = maskBuffer
            };
            items.Add(item);
        }
    }

    public void Paint(Texture2D albedoTex, Texture2D normalTex, Texture2D maskTex, Vector3 position, Vector3 normal, Vector3 tangent, Vector3 scale)
    {
        foreach (var item in items)
        {
            var query = new PaintQuery()
            {
                albedoBuffer = item.albedoBuffer,
                normalBuffer = item.normalBuffer,
                maskBuffer = item.maskBuffer,
                renderers = item.renderers,
                material = item.material,
                albedoTex = albedoTex,
                normalTex = normalTex,
                maskTex = maskTex,
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
            // Create new material for buffering render resource
            var material = CoreUtils.CreateEngineMaterial(shader);
            material.SetTexture("_AlbedoTex", albedoBuffer);
            material.SetTexture("_NormalTex", normalBuffer);
            material.SetTexture("_MaskTex", maskBuffer);
            material.SetTexture("_DecalAlbedoTex", query.albedoTex);
            material.SetTexture("_DecalNormalTex", query.normalTex);
            material.SetTexture("_DecalMaskTex", query.maskTex);
            material.SetVector("_DecalPosition", query.position);
            material.SetVector("_DecalNormal", query.normal);
            material.SetVector("_DecalTangent", query.tangent);
            material.SetVector("_DecalScale", query.scale);

            // Register Albedo and Normal, Mask drawcall if available
            var drawcalls = new List<Drawcall>();
            ctx.cmd.Blit(query.albedoBuffer, albedoBuffer);
            drawcalls.Add(new Drawcall() { buffer = query.albedoBuffer, pass = 0 });
            ctx.cmd.Blit(query.normalBuffer, normalBuffer);
            drawcalls.Add(new Drawcall() { buffer = query.normalBuffer, pass = 1 });
            ctx.cmd.Blit(query.maskBuffer, maskBuffer);
            drawcalls.Add(new Drawcall() { buffer = query.maskBuffer, pass = 2 });

            // Draw decal on texture
            foreach (var drawcall in drawcalls)
            {
                CoreUtils.SetRenderTarget(ctx.cmd, drawcall.buffer, ClearFlag.Color);

                foreach (var renderer in query.renderers)
                {
                    for (var i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        if (renderer.sharedMaterials[i].GetInstanceID() == query.material.GetInstanceID())
                        {
                            ctx.cmd.DrawRenderer(renderer, material, i, drawcall.pass);
                        }
                    }
                }
            }

            CoreUtils.Destroy(material);
            foreach (var drawcall in drawcalls) RTHandles.Release(drawcall.readBuffer);
        }
        queue.Clear();
    }

    protected override void Cleanup()
    {
        RTHandles.Release(albedoBuffer);
        RTHandles.Release(normalBuffer);
        RTHandles.Release(maskBuffer);

        foreach (var item in items)
        {
            if (item.albedoBuffer != null) RTHandles.Release(item.albedoBuffer);
            if (item.normalBuffer != null) RTHandles.Release(item.normalBuffer);
            if (item.maskBuffer != null) RTHandles.Release(item.maskBuffer);
        }

        queue.Clear();
        items.Clear();
    }
}
