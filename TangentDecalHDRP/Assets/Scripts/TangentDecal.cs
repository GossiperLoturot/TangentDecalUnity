using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;

[Serializable]
public class ItemDescriptor
{
    public Material material;
    public List<Renderer> renderers;
    public int width = 1024;
    public int height = 1024;
}

public class Item
{
    public RTHandle albedoBuffer;
    public RTHandle normalBuffer;
    public RTHandle maskBuffer;
    public Material material;
    public List<Renderer> renderers;
}

class DecalQuery
{
    public RTHandle albedoBuffer;
    public RTHandle normalBuffer;
    public RTHandle maskBuffer;
    public Material material;
    public List<Renderer> renderers;
    public Texture albedoTex;
    public Texture normalTex;
    public Texture maskTex;
    public Vector3 position;
    public Vector3 normal;
    public Vector3 tangent;
    public Vector3 scale;
}

public class TangentDecal : MonoBehaviour
{
    public int maxWidth = 1024;
    public int maxHeight = 1024;
    public string decalAlbedoTexName = "_DecalAlbedoMap";
    public string decalNormalTexName = "_DecalNormalMap";
    public string decalMaskTexName = "_DecalMaskMap";
    public List<ItemDescriptor> itemDescriptors = new List<ItemDescriptor>();

    List<Item> items = new List<Item>();
    CustomPassVolume volume;

    void OnEnable()
    {
        Debug.Assert(maxWidth > 0, "maxWidth must be greater than zero");
        Debug.Assert(maxHeight > 0, "maxHeight must be greater than zero");

        volume = gameObject.AddComponent<CustomPassVolume>();
        volume.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;

        var pass = volume.AddPassOfType<TangentDecalPass>();
        pass.width = maxWidth;
        pass.height = maxHeight;

        foreach (var itemDescriptor in itemDescriptors)
        {
            var width = Math.Min(itemDescriptor.width, maxWidth);
            var height = Math.Min(itemDescriptor.height, maxHeight);

            Debug.Assert(width > 0, "width must be greater than zero");
            Debug.Assert(height > 0, "height must be greater than zero");

            var albedoBuffer = RTHandles.Alloc(width, height);
            var normalBuffer = RTHandles.Alloc(width, height);
            var maskBuffer = RTHandles.Alloc(width, height);

            itemDescriptor.material.SetTexture(decalAlbedoTexName, albedoBuffer);
            itemDescriptor.material.SetTexture(decalNormalTexName, normalBuffer);
            itemDescriptor.material.SetTexture(decalMaskTexName, maskBuffer);

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

    void OnDisable()
    {
        foreach (var decal in items)
        {
            if (decal.albedoBuffer != null) decal.albedoBuffer.Release();
            if (decal.normalBuffer != null) decal.normalBuffer.Release();
            if (decal.maskBuffer != null) decal.maskBuffer.Release();
        }

        items.Clear();

        if (volume != null) Destroy(volume);
    }

    public void Paint(
        Texture albedoTex,
        Texture normalTex,
        Texture maskTex,
        Vector3 position,
        Vector3 normal,
        Vector3 tangent,
        Vector3 scale
    ) {
        foreach (var item in items)
        {
            var query = new DecalQuery()
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
            TangentDecalPass.queries.Add(query);
        }
    }
}

class TangentDecalPass : CustomPass
{
    public static List<DecalQuery> queries = new List<DecalQuery>();

    public int width;
    public int height;

    Shader shader;
    RTHandle buffer;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        shader = Shader.Find("Hidden/TangentDecal");
        buffer = RTHandles.Alloc(width, height);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        foreach (var query in queries)
        {
            var targets = new []
            {
                (query.albedoBuffer, query.albedoTex),
                (query.normalBuffer, query.normalTex),
                (query.maskBuffer, query.maskTex)
            };

            foreach (var (targetBuffer, targetTex) in targets) {
                ctx.cmd.CopyTexture(targetBuffer, buffer);

                CoreUtils.SetRenderTarget(ctx.cmd, targetBuffer, ClearFlag.Color);

                var material = CoreUtils.CreateEngineMaterial(shader);
                material.SetTexture("_MainTex", buffer);
                material.SetTexture("_DecalTex", targetTex);
                material.SetVector("_DecalPosition", query.position);
                material.SetVector("_DecalNormal", query.normal);
                material.SetVector("_DecalTangent", query.tangent);
                material.SetVector("_DecalScale", query.scale);
                
                foreach (var renderer in query.renderers)
                {
                    var submeshCount = renderer.sharedMaterials.Length;

                    for (var i = 0; i < submeshCount; i++)
                    {
                        if (renderer.sharedMaterials[i].GetInstanceID() == query.material.GetInstanceID())
                        {
                            ctx.cmd.DrawRenderer(renderer, material, i);
                        }
                    }
                }

                CoreUtils.Destroy(material);
            }
        }

        queries.Clear();
    }

    protected override void Cleanup()
    {
        if (buffer != null) buffer.Release();
    }
}
