using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
public class ItemDescriptor
{
    public Material material;
    public string albedoTexName = "_BaseColorMap";
    public string normalTexName = "_NormalMap";
    public string maskTexName = "_MaskMap";
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

class InitQuery
{
    public RTHandle albedoBuffer;
    public RTHandle normalBuffer;
    public RTHandle maskBuffer;
    public Texture2D albedoTex;
    public Texture2D normalTex;
    public Texture2D maskTex;
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
    public RTHandle writeBuffer;
    public int pass;
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
    List<InitQuery> initQueue = new List<InitQuery>();
    List<PaintQuery> paintQueue = new List<PaintQuery>();
    Shader shader;

    public void Init(List<ItemDescriptor> itemDescriptors)
    {
        Cleanup();

        foreach (var itemDescriptor in itemDescriptors)
        {
            // Clone and asssign new material to all related renderer
            var material = UnityEngine.Object.Instantiate(itemDescriptor.material);
            foreach (var renderer in itemDescriptor.renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i].GetInstanceID() == itemDescriptor.material.GetInstanceID())
                    {
                        materials[i] = material;
                    }
                }
                renderer.materials = materials;
            }

            var item = new Item()
            {
                material = material,
                renderers = itemDescriptor.renderers,
            };
            var query = new InitQuery();

            // Clone and assign new Texture to material
            var albedoTex = itemDescriptor.material.GetTexture(itemDescriptor.albedoTexName);
            if (albedoTex != null && albedoTex is Texture2D)
            {
                item.albedoBuffer = RTHandlesAllocCopy(albedoTex);
                item.material.SetTexture(itemDescriptor.albedoTexName, item.albedoBuffer);

                query.albedoTex = (Texture2D)albedoTex;
                query.albedoBuffer = item.albedoBuffer;
            }
            var normalTex = itemDescriptor.material.GetTexture(itemDescriptor.normalTexName);
            if (normalTex != null && normalTex is Texture2D)
            {
                item.normalBuffer = RTHandlesAllocCopy(normalTex);
                item.material.SetTexture(itemDescriptor.normalTexName, item.normalBuffer);

                query.normalTex = (Texture2D)normalTex;
                query.normalBuffer = item.normalBuffer;
            }
            var maskTex = itemDescriptor.material.GetTexture(itemDescriptor.maskTexName);
            if (maskTex != null && maskTex is Texture2D)
            {
                item.maskBuffer = RTHandlesAllocCopy(maskTex);
                item.material.SetTexture(itemDescriptor.maskTexName, item.maskBuffer);

                query.maskTex = (Texture2D)maskTex;
                query.maskBuffer = item.maskBuffer;
            }

            items.Add(item);
            initQueue.Add(query);
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
            paintQueue.Add(query);
        }
    }

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        shader = Shader.Find("Hidden/TangentDecal");
    }

    protected override void Execute(CustomPassContext ctx)
    {
        // Initialize new texture
        foreach (var query in initQueue)
        {
            if (query.albedoBuffer != null)
            {
                ctx.cmd.Blit(query.albedoTex, query.albedoBuffer);
            }

            if (query.normalBuffer != null)
            {
                ctx.cmd.Blit(query.normalTex, query.normalBuffer);
            }

            if (query.maskBuffer != null)
            {
                ctx.cmd.Blit(query.maskTex, query.maskBuffer);
            }
        }
        initQueue.Clear();

        // Paint texture based on all related renderer
        foreach (var query in paintQueue)
        {
            // Create new material for buffering render resource
            var material = CoreUtils.CreateEngineMaterial(shader);
            material.SetTexture("_DecalAlbedoTex", query.albedoTex);
            material.SetTexture("_DecalNormalTex", query.normalTex);
            material.SetTexture("_DecalMaskTex", query.maskTex);
            material.SetVector("_DecalPosition", query.position);
            material.SetVector("_DecalNormal", query.normal);
            material.SetVector("_DecalTangent", query.tangent);
            material.SetVector("_DecalScale", query.scale);

            // Register Albedo and Normal, Mask drawcall if available
            var drawcalls = new List<Drawcall>();
            if (query.albedoBuffer != null)
            {
                var readBuffer = RTHandlesAllocCopy(query.albedoBuffer);
                ctx.cmd.Blit(query.albedoBuffer, readBuffer);

                material.SetTexture("_AlbedoTex", readBuffer);
                drawcalls.Add(new Drawcall() { readBuffer = readBuffer, writeBuffer = query.albedoBuffer, pass = 0 });
            }
            if (query.normalBuffer != null)
            {
                var readBuffer = RTHandlesAllocCopy(query.normalBuffer);
                ctx.cmd.Blit(query.normalBuffer, readBuffer);

                material.SetTexture("_NormalTex", readBuffer);
                drawcalls.Add(new Drawcall() { readBuffer = readBuffer, writeBuffer = query.normalBuffer, pass = 1 });
            }
            if (query.maskBuffer != null)
            {
                var readBuffer = RTHandlesAllocCopy(query.maskBuffer);
                ctx.cmd.Blit(query.maskBuffer, readBuffer);

                material.SetTexture("_MaskTex", readBuffer);
                drawcalls.Add(new Drawcall() { readBuffer = readBuffer, writeBuffer = query.maskBuffer, pass = 2 });
            }

            // Draw decal on texture
            foreach (var drawcall in drawcalls)
            {
                CoreUtils.SetRenderTarget(ctx.cmd, drawcall.writeBuffer, ClearFlag.Color);

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
        paintQueue.Clear();
    }

    protected override void Cleanup()
    {
        foreach (var item in items)
        {
            if (item.albedoBuffer != null) RTHandles.Release(item.albedoBuffer);
            if (item.normalBuffer != null) RTHandles.Release(item.normalBuffer);
            if (item.maskBuffer != null) RTHandles.Release(item.maskBuffer);
        }

        initQueue.Clear();
        paintQueue.Clear();
        items.Clear();
    }

    RTHandle RTHandlesAllocCopy(Texture tex)
    {
        return RTHandles.Alloc(width: tex.width, height: tex.height, format: GraphicsFormat.R8G8B8A8_UNorm, filterMode: tex.filterMode, wrapMode: tex.wrapMode, useMipMap: tex.mipmapCount > 1, mipMapBias: tex.mipMapBias, anisoLevel: tex.anisoLevel);
    }

    RTHandle RTHandlesAllocCopy(RTHandle handle)
    {
        var tex = handle.rt;
        return RTHandles.Alloc(width: tex.width, height: tex.height, format: GraphicsFormat.R8G8B8A8_UNorm, filterMode: tex.filterMode, wrapMode: tex.wrapMode);
    }
}
