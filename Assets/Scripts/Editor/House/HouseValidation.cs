using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MakeTrap.House;
public static class HouseValidation
{
    public static string Run()
    {
        var h=HouseGenerator.Find();Physics.SyncTransforms();
        var nodes=new Dictionary<Vector2Int,List<Vector3>>();float step=.25f;
        int nx=Mathf.CeilToInt((h.width+4)/step),nz=Mathf.CeilToInt((h.depth+5)/step);
        for(int x=0;x<=nx;x++)for(int z=0;z<=nz;z++)
        {
            var pos=h.transform.position+new Vector3(-h.width/2-2+x*step,10,-h.depth/2-2+z*step);
            foreach(var hit in Physics.RaycastAll(pos,Vector3.down,18,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.normal.y<.70f||hit.point.y>h.storeyHeight+.2f||hit.point.y < -h.storeyHeight-.2f)continue;
                var foot=hit.point;
                // Test the larger player's 1m diameter / 2m height, with a small floor clearance.
                if(Physics.CheckCapsule(foot+Vector3.up*.65f,foot+Vector3.up*1.63f,.48f,~0,QueryTriggerInteraction.Ignore))continue;
                var key=new Vector2Int(x,z);if(!nodes.ContainsKey(key))nodes[key]=new List<Vector3>();
                if(!nodes[key].Any(n=>Mathf.Abs(n.y-foot.y)<.1f))nodes[key].Add(foot);
            }
        }
        var all=nodes.Values.SelectMany(v=>v).ToArray();
        var start=all.OrderBy(p=>(p-(h.transform.position+new Vector3(0,0,h.depth/2+1))).sqrMagnitude).First();
        var visited=new HashSet<Vector3>{start};var parents=new Dictionary<Vector3,Vector3>();var queue=new Queue<Vector3>();queue.Enqueue(start);
        while(queue.Count>0)
        {
            var p=queue.Dequeue();var local=p-h.transform.position;
            var k=new Vector2Int(Mathf.RoundToInt((local.x+h.width/2+2)/step),Mathf.RoundToInt((local.z+h.depth/2+2)/step));
            foreach(var delta in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            if(nodes.TryGetValue(k+delta,out var adjacent))foreach(var n in adjacent)
            {
                if(Mathf.Abs(p.y-n.y)>.3f||visited.Contains(n))continue;
                visited.Add(n);parents[n]=p;queue.Enqueue(n);
            }
        }
        var report=new List<string>();
        foreach(var floor in new[]{"Basement_B1","FirstFloor_1F","SecondFloor_2F"})
        foreach(Transform room in h.transform.Find(floor+"/Rooms"))
        {
            var target=room.position;
            var end=visited.Where(n=>Mathf.Abs(n.y-target.y)<.25f).OrderBy(n=>(n-target).sqrMagnitude).FirstOrDefault();
            if(Vector3.Distance(end,target)>1.1f)throw new Exception("Unreachable room: "+floor+"/"+room.name+" nearest "+end);
            report.Add("PASS capsule route: "+floor+"/"+room.name);
        }
        foreach(var name in new[]{"NetworkKid","NetworkThief"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/"+name+".prefab");
            var go=new GameObject("ValidationController");go.transform.localScale=prefab.transform.localScale;
            var cc=go.AddComponent<CharacterController>();EditorUtility.CopySerialized(prefab.GetComponent<CharacterController>(),cc);
            try
            {
                foreach(float y in new[]{-h.storeyHeight,h.storeyHeight})
                {
                    var target=h.transform.position+new Vector3(0,y,-h.depth/2+.8f);
                    var end=visited.Where(n=>Mathf.Abs(n.y-y)<.25f).OrderBy(n=>(n-target).sqrMagnitude).First();
                    var route=new List<Vector3>{end};while(parents.TryGetValue(route.Last(),out var prev))route.Add(prev);route.Reverse();
                    cc.enabled=false;go.transform.position=start+Vector3.up*.12f;cc.enabled=true;Physics.SyncTransforms();
                    foreach(var point in route)
                    {
                        for(int i=0;i<30;i++)
                        {
                            var d=point-go.transform.position;d.y=0;if(d.magnitude<.07f)break;
                            cc.Move(d.normalized*.045f+Vector3.down*.04f);
                        }
                    }
                    if(Vector3.Distance(go.transform.position,end)>.5f)throw new Exception(name+" stair traversal failed: "+go.transform.position+" expected "+end);
                    report.Add("PASS CharacterController walk: "+name+" to "+y);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        report.Add("Reachable sampled positions: "+visited.Count+" / "+all.Length);
        return string.Join("\n",report);
    }
}

