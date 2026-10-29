using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NOVR.MotionControls;

// Own only material/texture copies. Installed pilot atlases are sampled in memory,
// never altered, written to disk, or distributed with the mod.
internal sealed class PilotHandMaterials : IDisposable
{
    private readonly List<Material> _materials=new();
    private readonly List<Texture2D> _textures=new();
    private readonly Dictionary<Texture,Texture2D> _enhanced=new();
    private readonly Texture2D _fabric, _gloveColor, _gloveNormal;
    internal PilotHandMaterials()
    {
        _fabric=Load("PilotFabricDetail.png",false,false);
        _gloveColor=Load("GloveColor.jpg",false);
        _gloveNormal=Load("GloveNormal.png",true,false);
        // Unity's desktop normal-map unpack expects X in alpha, Y in green.
        var pixels=_gloveNormal.GetPixels32();for(var i=0;i<pixels.Length;i++)pixels[i]=new Color32(255,pixels[i].g,255,pixels[i].r);
        _gloveNormal.SetPixels32(pixels);_gloveNormal.Apply(true,true);
    }
    private Texture2D Load(string file,bool linear,bool discard=true)
    {
        using var stream=typeof(PilotHandMaterials).Assembly.GetManifestResourceStream("NOVR."+file) ?? throw new InvalidDataException("Missing hand texture "+file);
        using var memory=new MemoryStream();stream.CopyTo(memory);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,linear){name="NOVR "+file,filterMode=FilterMode.Trilinear,anisoLevel=4,wrapMode=TextureWrapMode.Repeat};_textures.Add(texture);
        if(!texture.LoadImage(memory.ToArray(),discard))throw new InvalidDataException("Unreadable hand texture "+file);
        return texture;
    }
    internal Material Sleeve(Material native)
    {
        var material=new Material(native){name="NOVR detailed "+native.name};_materials.Add(material);
        var source=native.HasProperty("_BaseMap")?native.GetTexture("_BaseMap"):native.mainTexture;
        if(source!=null)
        {
            if(!_enhanced.TryGetValue(source,out var texture))_enhanced[source]=texture=Enhance(source);
            SetTexture(material,"_BaseMap",texture);SetTexture(material,"_MainTex",texture);
        }
        // Several stripped game shader variants ignore detail keywords. Bake a
        // restrained weave/local-contrast pass once into our own albedo copy.
        material.DisableKeyword("_DETAIL_MULX2");material.DisableKeyword("_DETAIL_SCALED");
        if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.55f);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.25f);
        return material;
    }
    private Texture2D Enhance(Texture source)
    {
        var size=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(source.width,source.height))*2,1024,2048);
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="NOVR enhanced pilot albedo",filterMode=FilterMode.Trilinear,anisoLevel=4};_textures.Add(texture);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,size,size),0,0);texture.Apply(false);
            var input=texture.GetPixels32();var output=new Color32[input.Length];var fabric=_fabric.GetPixels32();var fw=_fabric.width;var fh=_fabric.height;
            for(var y=0;y<size;y++)for(var x=0;x<size;x++)
            {
                var at=y*size+x;var c=input[at];var left=input[y*size+Mathf.Max(0,x-1)];var right=input[y*size+Mathf.Min(size-1,x+1)];var up=input[Mathf.Min(size-1,y+1)*size+x];var down=input[Mathf.Max(0,y-1)*size+x];
                var grain=(fabric[((y*4)%size)*fh/size*fw+((x*4)%size)*fw/size].r-128)*.12f;
                byte Channel(byte value,int neighbours)=>(byte)Mathf.Clamp(Mathf.RoundToInt(value+.35f*(value-neighbours*.25f)+grain),0,255);
                output[at]=new Color32(Channel(c.r,left.r+right.r+up.r+down.r),Channel(c.g,left.g+right.g+up.g+down.g),Channel(c.b,left.b+right.b+up.b+down.b),c.a);
            }
            texture.SetPixels32(output);texture.Apply(true,true);Debug.Log("[NOVR] Pilot sleeve texture copy: "+source.width+"x"+source.height+" -> "+size+"x"+size+" with restrained fabric detail; native atlas preserved.");return texture;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    internal Material Glove(Material native,Rect patch)
    {
        var material=new Material(native){name="NOVR pilot-matched glove"};_materials.Add(material);
        var color=PilotColor(native,patch);var mean=Mathf.Max(.03f,(color.r+color.g+color.b)/3);
        var tint=new Color(Mathf.Clamp(color.r/mean*1.1f,.6f,1.3f),Mathf.Clamp(color.g/mean,.6f,1.3f),Mathf.Clamp(color.b/mean*.78f,.5f,1.3f),1);
        SetColor(material,"_BaseColor",tint);SetColor(material,"_Color",tint);
        SetTexture(material,"_BaseMap",_gloveColor);SetTexture(material,"_MainTex",_gloveColor);SetTexture(material,"_BumpMap",_gloveNormal);
        foreach(var property in new[]{"_MetallicGlossMap","_OcclusionMap","_EmissionMap","_DetailAlbedoMap","_DetailNormalMap","_DetailMask"})SetTexture(material,property,null);
        foreach(var keyword in new[]{"_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_EMISSION","_DETAIL_MULX2","_DETAIL_SCALED"})material.DisableKeyword(keyword);
        if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.55f);
        if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.22f);
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.22f);
        material.EnableKeyword("_NORMALMAP");return material;
    }
    private static void SetColor(Material m,string name,Color c){if(m.HasProperty(name))m.SetColor(name,c);}
    private static void SetTexture(Material m,string name,Texture? t){if(m.HasProperty(name)){m.SetTexture(name,t);m.SetTextureScale(name,Vector2.one);m.SetTextureOffset(name,Vector2.zero);}}
    private static Color PilotColor(Material native,Rect patch)
    {
        var source=native.HasProperty("_BaseMap")?native.GetTexture("_BaseMap"):native.mainTexture;
        if(source==null)return new Color(.8f,.9f,.8f);
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Texture2D? image=null;
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;image=new Texture2D(64,64,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();
            var sum=Color.black;var count=0;
            for(var y=0;y<8;y++)for(var x=0;x<8;x++){var pixel=image.GetPixelBilinear(patch.x+patch.width*(x+.5f)/8,patch.y+patch.height*(y+.5f)/8);if(pixel.maxColorComponent<.05f)continue;sum+=pixel;count++;}
            return count>0?sum/count:Color.white;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);}
    }
    public void Dispose(){foreach(var m in _materials)Object.Destroy(m);foreach(var t in _textures)Object.Destroy(t);_materials.Clear();_textures.Clear();}
}
