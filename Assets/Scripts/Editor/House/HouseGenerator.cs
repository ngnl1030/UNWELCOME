using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using MakeTrap.House;

public class HouseGenerator : EditorWindow
{
    [MenuItem("Tools/House Generator")]
    public static void Open() => GetWindow<HouseGenerator>("House Generator");
    public void CreateGUI()
    {
        var scroll = new ScrollView(); rootVisualElement.Add(scroll);
        scroll.Add(new Label("3-storey house blockout | GameScene"));
        var house = Find();
        if (house != null)
        {
            var so = new SerializedObject(house);
            foreach(var name in new[]{"width","depth","storeyHeight","wallThickness","corridorWidth","stairWidth","stairRun","doorWidth","doorHeight","frontRoomFraction"})
                scroll.Add(new PropertyField(so.FindProperty(name)));
            scroll.Bind(so);
        }
        scroll.Add(new Button(()=>{ Generate(); rootVisualElement.Clear(); CreateGUI(); }){text="Generate / Regenerate House"});
        if(house != null) foreach(Transform child in house.transform)
        {
            if(child.GetComponent<GeneratedHousePart>()==null) continue;
            var target=child.gameObject; var toggle=new Toggle(target.name){value=target.activeSelf};
            toggle.RegisterValueChangedCallback(e=>{Undo.RecordObject(target,"House visibility");target.SetActive(e.newValue);EditorSceneManager.MarkSceneDirty(target.scene);});
            scroll.Add(toggle);
        }
        scroll.Add(new Label("Edits apply on regeneration. Save scene after inspection.\nOnly generated branches are replaced; existing gameplay objects stay unchanged."));
    }
    public static HouseLayout Find() => UnityEngine.Object.FindObjectsByType<HouseLayout>(FindObjectsInactive.Include).FirstOrDefault();
    static HouseLayout s;
    static float X,Z,H,C,F,T;
    static Material wall, floor, basement, trim, roof, stair;
    static Transform Group(Transform parent,string name,bool managed=false)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<GeneratedHousePart>(); if(managed)Undo.RegisterCreatedObjectUndo(go,"Generate house branch"); return go.transform;
    }
    static Material Mat(string name,Color color)
    {
        const string dir="Assets/Materials/HouseBlockout";
        System.IO.Directory.CreateDirectory(dir);
        var path=dir+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;AssetDatabase.CreateAsset(mat,path);}
        return mat;
    }
    static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.AddComponent<GeneratedHousePart>();go.transform.SetParent(parent,false);
        go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Slab(Transform p,string name,float x0,float x1,float z0,float z1,float top,float thickness,Material mat)
    {if(x1>x0+.001f && z1>z0+.001f)Box(p,name,new Vector3((x0+x1)/2,top-thickness/2,(z0+z1)/2),new Vector3(x1-x0,thickness,z1-z0),mat);}
    static void Deck(Transform p,float top,bool leftHole,bool rightHole,float thickness,Material mat)
    {
        if(!leftHole&&!rightHole){Slab(p,"Slab",-X,X,-Z,Z,top,thickness,mat);return;}
        float a=leftHole?-C:0,b=leftHole?0:C;
        Slab(p,"West",-X,a,-Z,Z,top,thickness,mat);Slab(p,"East",b,X,-Z,Z,top,thickness,mat);
        Slab(p,"RearLanding",a,b,-Z,-s.stairRun,top,thickness,mat);Slab(p,"FrontLanding",a,b,0,Z,top,thickness,mat);
    }
    // Wall assembled around real rectangular openings, with no collider spanning the aperture.
    static void Wall(Transform parent,string name,bool alongX,float fixedAxis,float start,float end,float y,params Vector3[] holes)
    {
        var p=Group(parent,name);float cursor=start;
        Action<float,float,float,float,string> piece=(a,b,bottom,top,label)=>{
            if(b-a<.001f||top-bottom<.001f)return;
            Box(p,label,alongX?new Vector3((a+b)/2,y+(bottom+top)/2,fixedAxis):new Vector3(fixedAxis,y+(bottom+top)/2,(a+b)/2),
                alongX?new Vector3(b-a,top-bottom,T):new Vector3(T,top-bottom,b-a),wall);
        };
        foreach(var hole in holes.OrderBy(h=>h.x))
        {
            float a=hole.x-hole.y/2,b=hole.x+hole.y/2;
            piece(cursor,a,0,H-.2f,"Pier");
            float sill=hole.z==0?0:1.05f,head=hole.z==0?s.doorHeight:H-.55f;
            piece(a,b,0,sill,"Sill");piece(a,b,head,H-.2f,"Header");cursor=b;
        }
        piece(cursor,end,0,H-.2f,"Pier");
    }
    static Vector3 Door(float center)=>new Vector3(center,s.doorWidth,0);
    static Vector3 Window(float center)=>new Vector3(center,1.8f,1);
    static void Room(Transform root,string name,float x,float z,float y)
    {
        var p=Group(root,name);p.localPosition=new Vector3(x,y,z);
        var light=new GameObject("BlockoutLight").AddComponent<Light>();light.transform.SetParent(p,false);light.transform.localPosition=new Vector3(0,H-.6f,0);
        light.gameObject.AddComponent<GeneratedHousePart>();light.type=LightType.Point;light.range=9;light.intensity=1.4f;light.shadows=LightShadows.None;
    }
    static void Rail(Transform parent,float x,float y)
    {Box(parent,"StairwellGuard",new Vector3(x,y+.5f,-s.stairRun/2),new Vector3(.12f,1,s.stairRun),trim);}
    static void Stairs(Transform p,float centerX,float lowY,bool climbsRear)
    {
        float run=s.stairRun,angle=Mathf.Atan2(H,run)*Mathf.Rad2Deg;
        var ramp=Box(p,"WalkableSlope",new Vector3(centerX,lowY+H/2-.075f/Mathf.Cos(angle*Mathf.Deg2Rad),-run/2),new Vector3(s.stairWidth,.15f,Mathf.Sqrt(run*run+H*H)),stair);
        ramp.transform.localRotation=Quaternion.Euler(climbsRear?angle:-angle,0,0);
        ramp.GetComponent<Renderer>().enabled=false;
        int count=Mathf.CeilToInt(H/.16f);
        for(int i=0;i<count;i++)
        {
            float z=climbsRear?-(i+.5f)*run/count:-run+(i+.5f)*run/count;
            Box(p,"Tread_"+i,new Vector3(centerX,lowY+(i+1)*H/count-.05f,z),new Vector3(s.stairWidth,.1f,run/count+.01f),stair,false);
        }
        foreach(float dx in new[]{-s.stairWidth/2,s.stairWidth/2})
        {
            var rail=Box(p,"Handrail",new Vector3(centerX+dx,lowY+H/2+.9f,-run/2),new Vector3(.10f,.12f,Mathf.Sqrt(run*run+H*H)),trim);
            rail.transform.localRotation=ramp.transform.localRotation;
        }
    }
    public static void Generate()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="GameScene")throw new InvalidOperationException("Open existing GameScene first.");
        s=Find();
        if(s==null){var go=new GameObject("House_Root");Undo.RegisterCreatedObjectUndo(go,"Create House");s=go.AddComponent<HouseLayout>();go.transform.position=new Vector3(0,0,-14);}
        X=s.width/2;Z=s.depth/2;H=s.storeyHeight;T=s.wallThickness;C=s.stairWidth+.6f;F=Z-s.depth*s.frontRoomFraction;
        if(s.width<16||s.depth<13||H<3.1f||H>4||T<.1f||T>.35f||s.corridorWidth<2.6f||s.corridorWidth>4||s.stairWidth<2||s.stairWidth>2.8f||s.stairRun<5||s.stairRun>Z-1.2f||F<.8f||F>2.3f||s.doorWidth<1.5f||s.doorWidth>2.2f||s.doorHeight<2.4f||s.doorHeight>H-.3f)
            throw new InvalidOperationException("Unsafe dimensions. Use width >=16, depth >=13, height 3.1–4, wall .1–.35, corridor 2.6–4, stairs width 2–2.8/run 5 to depth/2−1.2, front split .8–2.3m, doors 1.5–2.2 x 2.4 to height−.3.");
        wall=Mat("WarmPlaster",new Color(.77f,.75f,.67f));floor=Mat("TimberBlockout",new Color(.47f,.32f,.20f));basement=Mat("Concrete",new Color(.39f,.44f,.46f));trim=Mat("DarkTrim",new Color(.18f,.23f,.26f));roof=Mat("Roof",new Color(.23f,.28f,.32f));stair=Mat("Stairs",new Color(.62f,.49f,.30f));
        foreach(var child in s.GetComponentsInChildren<Transform>(true))
            if(child!=s.transform && child.GetComponent<GeneratedHousePart>()==null && child.parent.GetComponent<GeneratedHousePart>()!=null)
                Undo.SetTransformParent(child,s.transform,"Preserve custom house content");
        foreach(Transform child in s.transform.Cast<Transform>().ToArray())if(child.GetComponent<GeneratedHousePart>()!=null)Undo.DestroyObjectImmediate(child.gameObject);
        for(int level=-1;level<=1;level++)
        {
            float y=level*H;var root=Group(s.transform,level==-1?"Basement_B1":level==0?"FirstFloor_1F":"SecondFloor_2F",true);
            var floors=Group(root,"Floors");var walls=Group(root,"Walls");var ceilings=Group(root,"Ceiling");var stairs=Group(root,"Stairs");var rooms=Group(root,"Rooms");
            Deck(floors,y,level==0,level==1,.1f,level==-1?basement:floor);
            Deck(ceilings,y+H-.1f,level==-1,level==0,.1f,wall);
            if(level==-1)
            {
                Wall(walls,"Front",true,Z,-X,X,y);Wall(walls,"Rear",true,-Z,-X,X,y);
                Wall(walls,"West",false,-X,-Z,Z,y);Wall(walls,"East",false,X,-Z,Z,y);
                Wall(walls,"StorageDivider",false,-C,-Z,Z,y,Door(2));
                Wall(walls,"ServiceDivider",false,C,-Z,Z,y,Door(-2.5f),Door(3));
                Wall(walls,"ServicePartition",true,0,C,X,y);
                Room(rooms,"LargeStorage",-(X+C)/2,0,y);Room(rooms,"MechanicalRoom",(X+C)/2,Z/2,y);Room(rooms,"SecretTreasureRoom",(X+C)/2,-Z/2,y);
                Stairs(stairs,-C/2,-H,false);
            }
            else
            {
                Wall(walls,"Front",true,Z,-X,X,y,Window(-X*.65f),level==0?Door(0):Window(0),Window(X*.65f));
                Wall(walls,"Rear",true,-Z,-X,X,y,Window(-X*.65f),Window(X*.65f));
                Wall(walls,"West",false,-X,-Z,Z,y,Window(-Z*.55f),Window(Z*.55f));Wall(walls,"East",false,X,-Z,Z,y,Window(-Z*.55f),Window(Z*.55f));
                float corridor=s.corridorWidth/2;
                Wall(walls,"FrontWestHall",false,-corridor,F,Z,y,Door((Z+F)/2));Wall(walls,"FrontEastHall",false,corridor,F,Z,y,Door((Z+F)/2));
                Wall(walls,"WestRoomPartition",true,F,-X,-corridor,y,Door(-(X+C)/2));Wall(walls,"EastRoomPartition",true,F,corridor,X,y,Door((X+C)/2));
                Wall(walls,"RearWestHall",false,-C,-Z,F,y,Door((-Z-s.stairRun)/2));Wall(walls,"RearEastHall",false,C,-Z,F,y,Door((-Z-s.stairRun)/2));
                Room(rooms,level==0?"LivingRoom":"MasterBedroom",-(X+corridor)/2,(Z+F)/2,y);
                Room(rooms,level==0?"DiningRoom":"KidsBedroom",(X+corridor)/2,(Z+F)/2,y);
                Room(rooms,level==0?"Study":"GuestBedroom",-(X+C)/2,(F-Z)/2,y);
                if(level==0){Room(rooms,"Kitchen",(X+C)/2,(F-Z)/2,y);Stairs(stairs,C/2,0,true);Rail(stairs,-C,0);Rail(stairs,0,0);}
                else{Wall(walls,"BathroomPartition",true,-Z*.45f,C,X,y,Door((X+C)/2));Room(rooms,"Bathroom",(X+C)/2,-Z*.72f,y);Room(rooms,"Playroom",(X+C)/2,-Z*.1f,y);Rail(stairs,0,y);Rail(stairs,C,y);}
            }
            Room(rooms,level==0?"EntranceHall":"ConnectingHall",0,Z*.55f,y);
            Room(rooms,"RearLanding",0,-Z+.65f,y);
        }
        var r=Group(s.transform,"Roof",true);
        float rise=1.8f,angleRoof=Mathf.Atan2(rise,X+.4f)*Mathf.Rad2Deg;
        foreach(int side in new[]{-1,1}){var panel=Box(r,"GableSlope",new Vector3(side*(X+.4f)/2,2*H+rise/2,0),new Vector3(Mathf.Sqrt((X+.4f)*(X+.4f)+rise*rise),.18f,s.depth+.8f),roof);panel.transform.localRotation=Quaternion.Euler(0,0,-side*angleRoof);}
        Group(s.transform,"Doors_Windows",true);Group(s.transform,"Furniture_Placeholders",true);
        var exterior=Group(s.transform,"ExteriorGround",true);
        // Preserve original floor intact, replacing it with slabs around the basement void.
        if(s.preservedGround==null)s.preservedGround=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Playground");
        if(s.preservedGround!=null){Undo.RecordObject(s.preservedGround,"Preserve original ground");s.preservedGround.SetActive(false);}
        Slab(exterior,"WestGround",-35,-X,-35,40,0,.2f,basement);Slab(exterior,"EastGround",X,35,-35,40,0,.2f,basement);
        Slab(exterior,"FrontGround",-X,X,Z,40,0,.2f,basement);Slab(exterior,"RearGround",-X,X,-35,-Z,0,.2f,basement);
        AssetDatabase.SaveAssets();EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(scene);Physics.SyncTransforms();Selection.activeGameObject=s.gameObject;
    }
}


