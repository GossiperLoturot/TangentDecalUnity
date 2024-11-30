using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

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

class Item
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

class PassData
{
    public Material material;
    public List<Renderer> renderers;
    public Material overrideMaterial;
    public int shaderPass;
}

class Drawcall
{
    public RTHandle mainRT;
    public Texture2D decalTex;
    public int shaderPass;
}

public class TangentDecal : MonoBehaviour
{
    public Shader shader;
    public List<ItemDescriptor> itemDescriptors = new List<ItemDescriptor>();

    TangentDecalPass pass;

    void OnEnable()
    {
        pass = new TangentDecalPass(shader, itemDescriptors);
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        pass.Dispose();
    }

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
    }

    public void Paint(Texture2D albedoTex, Texture2D normalTex, Texture2D maskTex, Texture2D alphaTex, Vector3 position, Vector3 normal, Vector3 tangent, Vector3 scale)
    {
        pass.Paint(albedoTex, normalTex, maskTex, alphaTex, position, normal, tangent, scale);
    }
}

class TangentDecalPass : ScriptableRenderPass
{
    Shader shader;
    List<Item> items = new List<Item>();
    List<PaintQuery> queue = new List<PaintQuery>();
    List<Material> overrideMaterials = new List<Material>();

    public TangentDecalPass(Shader shader, List<ItemDescriptor> itemDescriptors)
    {
        this.shader = shader;

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

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        foreach (var query in queue)
        {
            var drawcalls = new Drawcall[]
            {
                new Drawcall() { mainRT = query.albedoRT, decalTex = query.albedoTex, shaderPass = 0 },
                new Drawcall() { mainRT = query.normalRT, decalTex = query.normalTex, shaderPass = 1 },
                new Drawcall() { mainRT = query.maskRT, decalTex = query.maskTex, shaderPass = 2 },
                new Drawcall() { mainRT = query.alphaRT, decalTex = query.alphaTex, shaderPass = 3 }
            };

            foreach (var drawcall in drawcalls)
            {
                // Register Albedo and Normal, Mask drawcall if available
                var textureHandle = renderGraph.ImportTexture(drawcall.mainRT);
                var textureHandleDesc = renderGraph.GetTextureDesc(textureHandle);
                textureHandleDesc.name = "Target Decal Texture";
                textureHandleDesc.clearBuffer = false;
                textureHandleDesc.msaaSamples = MSAASamples.None;
                textureHandleDesc.depthBufferBits = 0;

                var bufferTextureHandle = renderGraph.CreateTexture(textureHandleDesc);

                // Create new material for buffering render resource
                var overrideMaterial = CoreUtils.CreateEngineMaterial(shader);
                overrideMaterial.SetTexture("_MainTex", drawcall.mainRT);
                overrideMaterial.SetTexture("_DecalTex", drawcall.decalTex);
                overrideMaterial.SetVector("_DecalPosition", query.position);
                overrideMaterial.SetVector("_DecalNormal", query.normal);
                overrideMaterial.SetVector("_DecalTangent", query.tangent);
                overrideMaterial.SetVector("_DecalScale", query.scale);
                overrideMaterials.Add(overrideMaterial);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Compute Tangent Decal", out var passData))
                {
                    passData.material = query.material;
                    passData.renderers = query.renderers;
                    passData.overrideMaterial = overrideMaterial;
                    passData.shaderPass = drawcall.shaderPass;

                    builder.SetRenderAttachment(bufferTextureHandle, 0, AccessFlags.Write);
                    builder.UseTexture(textureHandle, AccessFlags.Read);
                    builder.SetRenderFunc(static (PassData passData, RasterGraphContext ctx) =>
                    {
                        foreach (var renderer in passData.renderers)
                        {
                            for (var i = 0; i < renderer.sharedMaterials.Length; i++)
                            {
                                if (renderer.sharedMaterials[i].GetInstanceID() == passData.material.GetInstanceID())
                                {
                                    ctx.cmd.DrawRenderer(renderer, passData.overrideMaterial, i, passData.shaderPass);
                                }
                            }
                        }
                    });
                }

                renderGraph.AddBlitPass(bufferTextureHandle, textureHandle, Vector2.one, Vector2.zero);
            }
        }
        queue.Clear();
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        foreach (var overrideMaterial in overrideMaterials)
        {
            CoreUtils.Destroy(overrideMaterial);
        }
        overrideMaterials.Clear();
    }

    public void Dispose()
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
