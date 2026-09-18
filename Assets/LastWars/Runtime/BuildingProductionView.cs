using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastWars.Client
{
    public sealed class BuildingProductionView : MonoBehaviour
    {
        sealed class Badge { public RectTransform Root, Fill; public TMP_Text Label; public ResourceIcon Icon; public BuildingDto Building; public float Height; }
        readonly List<Badge> badges = new List<Badge>();
        readonly Dictionary<string, ProductionDto> production = new Dictionary<string, ProductionDto>();
        RectTransform layer;
        BaseWorld world;
        double received;
        public bool Hidden;
        public bool HasSnapshot { get; private set; }
        public void Initialize(BaseWorld value)
        {
            world=value;
            layer=new GameObject("Building production indicators",typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(transform,false); layer.SetAsFirstSibling();
            layer.anchorMin=Vector2.zero; layer.anchorMax=Vector2.one; layer.offsetMin=layer.offsetMax=Vector2.zero;
        }
        public void Snapshot(ProductionStorageDto value)
        {
            production.Clear(); HasSnapshot=false;
            if(value?.local_production==null) return;
            foreach(var item in value.local_production)
                if(item!=null && !string.IsNullOrEmpty(item.building_id)) production[item.building_id]=item;
            received=Time.realtimeSinceStartupAsDouble; HasSnapshot=true;
        }
        public bool Ready(string id) => production.TryGetValue(id,out var value) && ProductionRules.Evaluate(value,Time.realtimeSinceStartupAsDouble-received).Ready;
        public void Render(BaseDto data)
        {
            foreach(var badge in badges) { badge.Root.gameObject.SetActive(false); Destroy(badge.Root.gameObject); }
            badges.Clear();
            foreach(var b in data.buildings)
            {
                if(!Contracts.Produces(b.type) || b.level<=0) continue;
                var root=Rect("Production "+b.id,layer,new Vector2(84,36));
                root.gameObject.AddComponent<Image>().color=new Color(.045f,.075f,.11f,.92f);
                root.GetComponent<Image>().raycastTarget=false;
                var label=FrontierView.Text(root,"Sincronizando",14,24);
                label.alignment=TextAlignmentOptions.Center;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);
                label.rectTransform.anchoredPosition=Vector2.zero; label.rectTransform.sizeDelta=new Vector2(80,20);
                var track=Rect("Track",root,Vector2.zero);
                track.anchorMin=Vector2.zero; track.anchorMax=Vector2.one; track.offsetMin=track.offsetMax=Vector2.zero;
                track.gameObject.AddComponent<Image>().color=new Color(.2f,.25f,.3f); track.GetComponent<Image>().raycastTarget=false;
                var fill=Rect("Fill",track,Vector2.zero); fill.anchorMin=Vector2.zero; fill.anchorMax=Vector2.one; fill.offsetMin=fill.offsetMax=Vector2.zero;
                fill.gameObject.AddComponent<Image>().color=new Color(.13f,.53f,.27f); fill.GetComponent<Image>().raycastTarget=false;
                var icon=ResourceIcon.Create(root,ProductionRules.Kind(b.type),new Vector2(30,30));
                icon.rectTransform.anchoredPosition=new Vector2(-24,0);
                label.fontStyle=FontStyles.Bold;
                label.transform.SetAsLastSibling();
                float height=0;
                var model=world.BuildingModel(b.id);
                if(model!=null) foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                    height=Mathf.Max(height,renderer.bounds.max.y-model.transform.position.y);
                badges.Add(new Badge {Root=root,Fill=fill,Label=label,Icon=icon,Building=b,Height=height});
            }
        }
        void LateUpdate()
        {
            if(layer==null || world==null) return;
            layer.gameObject.SetActive(!Hidden);
            if(Hidden) return;
            foreach(var badge in badges)
            {
                var model=world.BuildingModel(badge.Building.id);
                if(model==null) { badge.Root.gameObject.SetActive(false); continue; }
                var pos=model.transform.position; float top=pos.y+badge.Height;
                var screen=world.ViewCamera.WorldToScreenPoint(new Vector3(pos.x,top+.3f,pos.z));
                badge.Root.gameObject.SetActive(screen.z>0);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,screen,null,out var local);
                badge.Root.anchoredPosition=local+new Vector2(0,18);
                production.TryGetValue(badge.Building.id,out var value);
                var state=ProductionRules.Evaluate(value,Time.realtimeSinceStartupAsDouble-received);
                badge.Icon.gameObject.SetActive(state.Ready);
                badge.Fill.parent.gameObject.SetActive(!state.Ready && state.Active);
                badge.Fill.anchorMax=new Vector2(state.Progress,1);
                badge.Label.rectTransform.anchoredPosition=state.Ready?new Vector2(17,0):Vector2.zero;
                badge.Label.rectTransform.sizeDelta=state.Ready?new Vector2(49,28):new Vector2(80,20);
                badge.Label.text=state.Ready?"Coletar":!HasSnapshot?"Sem conexão":!state.Active?"Sem produção":state.Seconds+" s";
            }
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;return r;
        }
    }
}
