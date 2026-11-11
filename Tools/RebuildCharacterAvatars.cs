// Run from the project root: unity command eval_file --file Tools/RebuildCharacterAvatars.cs --format json
// Retain importer T-pose rotations while correcting its out-of-scale skeleton positions.
// Using bind-pose rotations here makes retargeted arms cross behind the back.
if(EditorApplication.isPlaying)throw new System.Exception("Stop play first");
var report=new System.Collections.Generic.List<string>();
foreach(var name in new[]{"KID_Boy","KID_Girl","Thief_Fat","Thief_Thin"}){
 var folder="Assets/Characters/ThirdPerson/"+(name.StartsWith("KID")?"Kids/":"Thieves/")+name;
 var path=folder+"/Model/"+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);var d=importer.humanDescription;
 var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));rig.name=AssetDatabase.LoadAssetAtPath<GameObject>(path).name;
 try{
 var rotations=d.skeleton.ToDictionary(b=>b.name,b=>b.rotation);
 foreach(var t in rig.GetComponentsInChildren<Transform>())if(rotations.TryGetValue(t.name,out var rotation))t.localRotation=rotation;
 d.skeleton=rig.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 var corrected=AvatarBuilder.BuildHumanAvatar(rig,d);if(!corrected.isValid||!corrected.isHuman)throw new System.Exception(name+" invalid avatar");
 var existing=AssetDatabase.LoadAssetAtPath<Avatar>(folder+"/Rig/"+name+"Avatar.asset");corrected.name=existing.name;EditorUtility.CopySerialized(corrected,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(corrected);
 report.Add(name+": actual bone lengths + importer T-pose rotations");
 }finally{UnityEngine.Object.DestroyImmediate(rig);}
}
AssetDatabase.SaveAssets();return string.Join("\n",report);
