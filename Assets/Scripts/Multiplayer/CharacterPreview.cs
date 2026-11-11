using UnityEngine;
using UnityEngine.UI;
namespace MakeTrap.Multiplayer
{
    public class CharacterPreview : MonoBehaviour
    {
        public GameObject visualPrefab;
        public GameObject[] variants;
        public RawImage display;
        public int previewIndex;
        [Range(0.1f, 1f)] public float framingScale = 1f;
        GameObject stage, visual;
        RenderTexture texture;
        Camera previewCamera;
        int selected = -1;
        Bounds bounds;
        void Start()
        {
            if(display==null)return;
            stage=new GameObject("PreviewStage"+previewIndex);stage.transform.position=new Vector3(1000+previewIndex*20,0,0);
            previewCamera=new GameObject("PreviewCamera",typeof(Camera)).GetComponent<Camera>();previewCamera.transform.SetParent(stage.transform);
            previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.065f,.09f,.11f);previewCamera.fieldOfView=35;previewCamera.farClipPlane=20;
            texture=new RenderTexture(384,512,24);previewCamera.targetTexture=texture;display.texture=texture;
            SetCharacter(previewIndex%2);
        }
        public void SetCharacter(int index)
        {
            if(stage==null||selected==index)return;
            var prefab=variants!=null&&index>=0&&index<variants.Length?variants[index]:visualPrefab;
            if(prefab==null)return;
            selected=index;
            if(visual!=null){visual.SetActive(false);Destroy(visual);}
            visual=Instantiate(prefab,stage.transform);visual.transform.localPosition=Vector3.zero;
            var animator=visual.GetComponent<Animator>();
            if(animator!=null){animator.fireEvents=false;animator.Rebind();animator.SetBool("Grounded",true);animator.Update(0);}
            bool first=true;bounds=new Bounds(stage.transform.position+Vector3.up,Vector3.one);
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>()) {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            // Preserve the framing when a smaller character should look smaller in its card.
            bounds.size /= Mathf.Max(.1f, framingScale);
        }
        void LateUpdate()
        {
            if(previewCamera==null||display==null)return;
            var rect=display.rectTransform.rect;if(rect.width<=0||rect.height<=0)return;
            previewCamera.aspect=rect.width/rect.height;
            float fit=Mathf.Max(bounds.size.y,bounds.size.x/previewCamera.aspect);
            float distance=fit*.55f/Mathf.Tan(previewCamera.fieldOfView*.5f*Mathf.Deg2Rad)+bounds.extents.z;
            previewCamera.transform.position=bounds.center+Vector3.forward*distance;
            previewCamera.transform.LookAt(bounds.center);
        }
        void OnDestroy(){if(stage!=null)Destroy(stage);if(texture!=null){texture.Release();Destroy(texture);}}
    }
}
