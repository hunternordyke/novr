using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Local render-only sleeves. The native animator, pilot and remote players are never modified.
[DefaultExecutionOrder(10000)]
internal sealed class PilotHandVisuals : MonoBehaviour
{
    private static PilotHandVisuals? _instance;
    private Aircraft? _aircraft;
    private Transform? _root;
    private SkinnedMeshRenderer? _source;
    private Transform[] _sourceBones=Array.Empty<Transform>(), _copies=Array.Empty<Transform>();
    private readonly RiggedGlove?[] _gloves=new RiggedGlove?[2];
    private readonly SkinnedMeshRenderer?[] _sleeves=new SkinnedMeshRenderer?[2];
    private readonly int[,] _arm=new int[2,3];
    private readonly List<Mesh> _ownedMeshes=new();
    private Material? _fallback;
    private PilotHandMaterials? _materials;
    private float _nextRetry;
    private float _sleeveThickness = -1;
    private Vector3[]? _sleeveVertices;
    private Matrix4x4[]? _bindPoses;
    private int _attempts;
    internal static bool Visible(bool left) => _instance?._gloves[left?0:1]?.Root.gameObject.activeInHierarchy==true;
    private void Awake()=>_instance=this;
    private void LateUpdate()
    {
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft.cockpit==null || aircraft.cockpit.IsDetached()) { Release(); return; }
        if (_aircraft!=aircraft) { Release(); _aircraft=aircraft; _attempts=0; _nextRetry=0; }
        var settings=ModConfiguration.Instance.MotionControls;
        var camera=APIBus.MainCamera;
        var show=settings.RiggedGloves.Value && settings.Enabled.Value && CameraStateManager.cameraMode==CameraMode.cockpit && camera!=null && camera.transform.parent!=null && TrackedControllers.Available;
        if(!show) { if(_root!=null)_root.gameObject.SetActive(false);return; }
        if(_root==null)
        {
            if(_attempts>=3 || Time.unscaledTime<_nextRetry)return;
            _attempts++;_nextRetry=Time.unscaledTime+2;
            try { Create(aircraft,camera!.transform.parent!); }
            catch(Exception e) { ReleaseVisuals();Debug.LogWarning("[NOVR] Pilot hand visuals unavailable: "+e.Message);return; }
        }
        if(_root!.parent!=camera!.transform.parent)_root.SetParent(camera.transform.parent,false);_root.localPosition=Vector3.zero;_root.localRotation=Quaternion.identity;_root.localScale=Vector3.one;_root.gameObject.SetActive(true);
        if (_sleeveThickness != settings.SleeveThickness.Value) ResizeSleeves(settings.SleeveThickness.Value);
        var controls=MotionFlightController.Instance;
        var head=_root.TransformPoint(NOVRHeadsetData.Translation+CockpitProfiles.SeatOffset);
        // Translate the body with the seated viewer; independent cockpit controls keep their physical anchors.
        var shift=head-aircraft.cockpitViewPoint.position;
        if(_source!=null)
        {
            for(var i=0;i<_copies.Length;i++) { _copies[i].position=_sourceBones[i].position+shift;_copies[i].rotation=_sourceBones[i].rotation;_copies[i].localScale=_sourceBones[i].lossyScale; }
            foreach(var sleeve in _sleeves) if(sleeve!=null)
            { sleeve.transform.position=_source.transform.position+shift;sleeve.transform.rotation=_source.transform.rotation;sleeve.transform.localScale=_source.transform.lossyScale; }
        }
        for(var side=0;side<2;side++)
        {
            var left=side==0; var sample=TrackedControllers.Read(left);
            var held=left==settings.LeftHanded.Value?controls?.StickHeld==true:controls?.ThrottleHeld==true;
            _gloves[side]?.Pose(sample,held);
            if(_sleeves[side]!=null) _sleeves[side]!.enabled=settings.PilotArms.Value && sample.Valid && SolveArm(side,_gloves[side]!.Root.position);
        }
    }
    private void Create(Aircraft aircraft,Transform tracking)
    {
        _root=new GameObject("NOVR Pilot Hands").transform;_root.SetParent(tracking,false);
        // Pick the pilot nearest this cockpit seat, including multi-seat mod aircraft.
        var candidates=aircraft.pilots.Where(p=>p!=null).SelectMany(p=>p.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        _source=candidates.Where(r=>r.sharedMesh!=null&&r.bones.Any(b=>b!=null&&b.name=="hand_L")&&r.bones.Any(b=>b!=null&&b.name=="head")).OrderBy(r=>Vector3.Distance(r.bones.First(b=>b!=null&&b.name=="head").position,aircraft.cockpitViewPoint.position)).FirstOrDefault();
        var material=_source?.sharedMaterial;
        if(material==null) material=_fallback=CockpitMaterials.Opaque("NOVR fallback glove",new Color(.12f,.14f,.12f));
        _materials=new PilotHandMaterials();
        var patches=new[]{new Rect(0,0,1,1),new Rect(0,0,1,1)};
        if(_source!=null)
        {
            _sourceBones=_source.bones;
            var mesh=_source.sharedMesh;var weights=mesh.boneWeights;var uv=PilotMeshData.ReadUv(mesh);
            for(var side=0;side<2;side++)
            {
                var suffix=side==0?"_L":"_R";_arm[side,0]=Array.FindIndex(_sourceBones,b=>b.name=="upperarm"+suffix);_arm[side,1]=Array.FindIndex(_sourceBones,b=>b.name=="forearm"+suffix);_arm[side,2]=Array.FindIndex(_sourceBones,b=>b.name=="hand"+suffix);
                var hand=_arm[side,2];var min=new Vector2(1,1);var max=Vector2.zero;var found=false;
                for(var i=0;i<uv.Length;i++) if(Weight(weights[i],hand)>.8f){min=Vector2.Min(min,uv[i]);max=Vector2.Max(max,uv[i]);found=true;}
                if(found) {var margin=(max-min)*.12f;patches[side]=Rect.MinMaxRect(min.x+margin.x,min.y+margin.y,max.x-margin.x,max.y-margin.y);}
            }
            try { CreateSleeves(mesh,weights,uv); }
            catch(Exception e) { Debug.LogWarning("[NOVR] Pilot sleeves unavailable; tracked gloves remain available: "+e.Message); }
        }
        for(var side=0;side<2;side++) _gloves[side]=new RiggedGlove(side==0,_root,_materials.Glove(material,patches[side]),patches[side]);
        Debug.Log("[NOVR] Rigged Valve gloves ready; pilot material="+material.name+", sleeve renderers="+_sleeves.Count(s=>s!=null)+". Controller grip/trigger pose fingers; no finger-tracking claim.");
    }
    private void CreateSleeves(Mesh source,BoneWeight[] weights,Vector2[] uv)
    {
        if(source.vertexCount>20000 || weights.Length!=source.vertexCount || uv.Length!=source.vertexCount)return;
        var vertices=DashboardSurface.Read(source,VertexAttribute.Position);var normals=DashboardSurface.Read(source,VertexAttribute.Normal);if(vertices==null)return;
        var indices=PilotMeshData.Indices(source);
        _sleeveVertices=vertices; _bindPoses=source.bindposes; _sleeveThickness=-1;
        _copies=new Transform[_sourceBones.Length];
        for(var i=0;i<_copies.Length;i++){_copies[i]=new GameObject("Sleeve "+_sourceBones[i].name).transform;_copies[i].SetParent(_root,false);}
        for(var side=0;side<2;side++)
        {
            var upper=_arm[side,0];var fore=_arm[side,1];var hand=_arm[side,2];if(upper<0||fore<0||hand<0)continue;
            var keep=new bool[weights.Length];
            for(var i=0;i<keep.Length;i++)keep[i]=Weight(weights[i],upper)+Weight(weights[i],fore)>.45f && Weight(weights[i],hand)<.55f;
            var mesh=new Mesh{name="Installed pilot sleeve",vertices=vertices,uv=uv,boneWeights=weights,bindposes=source.bindposes,subMeshCount=indices.Length};_ownedMeshes.Add(mesh);
            for(var s=0;s<indices.Length;s++){var tris=new List<int>();for(var j=0;j+2<indices[s].Length;j+=3)if(keep[indices[s][j]]&&keep[indices[s][j+1]]&&keep[indices[s][j+2]]){tris.Add(indices[s][j]);tris.Add(indices[s][j+1]);tris.Add(indices[s][j+2]);}mesh.SetTriangles(tris,s);}
            if(normals!=null)mesh.normals=normals;else mesh.RecalculateNormals();mesh.RecalculateTangents();
            var go=new GameObject(side==0?"Pilot left sleeve":"Pilot right sleeve");go.transform.SetParent(_root,false);go.layer=(int)LayerHelper.Layers.Cockpit;
            var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.bones=_copies;r.rootBone=_copies[0];r.sharedMaterials=Array.ConvertAll(_source!.sharedMaterials,m=>_materials!.Sleeve(m));r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
            r.localBounds=new Bounds(source.bounds.center,Vector3.one*.04f);r.updateWhenOffscreen=false;_sleeves[side]=r;
        }
    }
    private void ResizeSleeves(float thickness)
    {
        _sleeveThickness=thickness;
        if(_sleeveVertices==null || _bindPoses==null)return;
        for(var side=0;side<2;side++)
        {
            var renderer=_sleeves[side];if(renderer==null)continue;
            Vector3 Bind(int index)=>_bindPoses[_arm[side,index]].inverse.MultiplyPoint3x4(Vector3.zero);
            var shoulder=Bind(0);var elbow=Bind(1);var wrist=Bind(2);var vertices=new Vector3[_sleeveVertices.Length];
            for(var i=0;i<vertices.Length;i++)
            {
                var point=_sleeveVertices[i];var upper=Closest(shoulder,elbow,point);var fore=Closest(elbow,wrist,point);
                var center=(point-upper).sqrMagnitude<(point-fore).sqrMagnitude?upper:fore;
                vertices[i]=center+(point-center)*thickness;
            }
            var mesh=renderer.sharedMesh;mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();
        }
    }
    private static Vector3 Closest(Vector3 a,Vector3 b,Vector3 p)
    { var direction=b-a;return a+direction*Mathf.Clamp01(Vector3.Dot(p-a,direction)/Mathf.Max(.000000001f,direction.sqrMagnitude)); }
    private bool SolveArm(int side,Vector3 target)
    {
        var u=_arm[side,0];var f=_arm[side,1];var h=_arm[side,2];if(u<0||f<0||h<0)return false;
        var upper=_copies[u];var fore=_copies[f];var hand=_copies[h];var shoulder=upper.position;
        var a=Vector3.Distance(upper.position,fore.position);var b=Vector3.Distance(fore.position,hand.position);var delta=target-shoulder;var distance=delta.magnitude;
        if(a<.05f||b<.05f||distance<.03f||distance>a+b-.002f)return false; // Gloves remain tracked beyond the sleeve's reachable envelope.
        var direction=delta/distance;var hint=_root!.TransformDirection(new Vector3(side==0?-.35f:.35f,-.5f,-.2f));
        var bend=Vector3.ProjectOnPlane(hint,direction).normalized;if(bend.sqrMagnitude<.5f)bend=Vector3.ProjectOnPlane(_root.right,direction).normalized;
        var along=(a*a-b*b+distance*distance)/(2*distance);var height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var elbow=shoulder+direction*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(fore.position-shoulder,elbow-shoulder)*upper.rotation;
        var oldHand=hand.position;var oldFore=fore.position;fore.position=elbow;fore.rotation=Quaternion.FromToRotation(oldHand-oldFore,target-elbow)*fore.rotation;hand.position=target;
        return TrackedControllers.Finite(elbow);
    }
    private static float Weight(BoneWeight w,int index)=>(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
    private void ReleaseVisuals()
    {
        for(var i=0;i<2;i++){_gloves[i]?.Dispose();_gloves[i]=null;_sleeves[i]=null;}
        foreach(var mesh in _ownedMeshes)Object.Destroy(mesh);_ownedMeshes.Clear();
        _materials?.Dispose();_materials=null;
        if(_fallback!=null)Object.Destroy(_fallback);_fallback=null;
        if(_root!=null)Object.Destroy(_root.gameObject);_root=null;_source=null;_copies=Array.Empty<Transform>();_sourceBones=Array.Empty<Transform>();_sleeveVertices=null;_bindPoses=null;_sleeveThickness=-1;
    }
    private void Release(){ReleaseVisuals();_aircraft=null;}
    private void OnDisable(){if(_root!=null)_root.gameObject.SetActive(false);}
    private void OnDestroy(){Release();if(_instance==this)_instance=null;}
}
