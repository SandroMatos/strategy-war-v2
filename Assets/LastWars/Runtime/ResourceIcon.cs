using UnityEngine;
using UnityEngine.UI;

namespace LastWars.Client
{
    // Original vector artwork: no font glyph dependency or external textures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResourceIcon : MaskableGraphic
    {
        public ResourceKind Kind;
        public bool Builder;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Builder)
            {
                // Hard hat, drawn with the same vector UI technique as the resource icons.
                Circle(mesh, new Vector2(.5f,.48f), .32f, new Color(1,.68f,.12f));
                Poly(mesh,new Color(.13f,.2f,.29f),new Vector2(.12f,.05f),new Vector2(.88f,.05f),new Vector2(.88f,.32f),new Vector2(.12f,.32f));
                Poly(mesh,new Color(1,.83f,.25f),new Vector2(.1f,.3f),new Vector2(.9f,.3f),new Vector2(.9f,.42f),new Vector2(.1f,.42f));
                Poly(mesh,new Color(1,.91f,.5f),new Vector2(.44f,.4f),new Vector2(.56f,.4f),new Vector2(.56f,.84f),new Vector2(.44f,.84f));
                return;
            }
            switch (Kind)
            {
                case ResourceKind.Food:
                    Poly(mesh, new Color(.4f,.66f,.2f), new Vector2(.47f,.1f),new Vector2(.54f,.1f),new Vector2(.54f,.86f),new Vector2(.47f,.86f));
                    for (int i=0;i<3;i++)
                    {
                        float y=.24f+i*.19f;
                        Poly(mesh,new Color(1,.76f,.2f),new Vector2(.49f,y),new Vector2(.23f,y+.08f),new Vector2(.2f,y+.26f),new Vector2(.45f,y+.2f));
                        Poly(mesh,new Color(1,.88f,.4f),new Vector2(.53f,y+.06f),new Vector2(.77f,y+.14f),new Vector2(.79f,y+.3f),new Vector2(.56f,y+.24f));
                    }
                    break;
                case ResourceKind.Iron:
                    Poly(mesh,new Color(.38f,.53f,.65f),new Vector2(.1f,.25f),new Vector2(.72f,.18f),new Vector2(.91f,.42f),new Vector2(.8f,.69f),new Vector2(.31f,.75f));
                    Poly(mesh,new Color(.77f,.89f,.98f),new Vector2(.2f,.43f),new Vector2(.72f,.36f),new Vector2(.8f,.69f),new Vector2(.31f,.75f));
                    break;
                case ResourceKind.Gold:
                    Circle(mesh,new Vector2(.5f,.48f),.38f,new Color(.77f,.43f,.06f));
                    Circle(mesh,new Vector2(.5f,.53f),.32f,new Color(1,.79f,.17f));
                    Poly(mesh,new Color(1,.95f,.57f),new Vector2(.5f,.31f),new Vector2(.65f,.53f),new Vector2(.5f,.76f),new Vector2(.35f,.53f));
                    break;
                case ResourceKind.Oil:
                    Circle(mesh,new Vector2(.5f,.37f),.29f,new Color(.31f,.24f,.48f));
                    Poly(mesh,new Color(.31f,.24f,.48f),new Vector2(.23f,.48f),new Vector2(.5f,.94f),new Vector2(.77f,.48f));
                    Poly(mesh,new Color(.76f,.64f,.96f),new Vector2(.34f,.32f),new Vector2(.39f,.24f),new Vector2(.45f,.27f),new Vector2(.38f,.56f));
                    break;
            }
        }
        public Mesh CreateMesh()
        {
            using (var vertices = new VertexHelper())
            {
                OnPopulateMesh(vertices);
                var mesh = new Mesh { name = Kind + " icon geometry" };
                vertices.FillMesh(mesh);
                return mesh;
            }
        }

        void Circle(VertexHelper m, Vector2 center, float radius, Color c)
        {
            var points = new Vector2[24];
            for(int i=0;i<points.Length;i++) { float a=i*Mathf.PI*2/points.Length; points[i]=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius; }
            Poly(m,c,points);
        }
        void Poly(VertexHelper m, Color c, params Vector2[] points)
        {
            var r=GetPixelAdjustedRect(); float size=Mathf.Min(r.width,r.height); int start=m.currentVertCount;
            foreach(var p in points) m.AddVert(new Vector3(r.center.x+(p.x-.5f)*size,r.center.y+(p.y-.5f)*size),c,Vector2.zero);
            for(int i=1;i<points.Length-1;i++) m.AddTriangle(start,start+i,start+i+1);
        }
        public static ResourceIcon Create(Transform parent, ResourceKind kind, Vector2 size)
        {
            var icon=new GameObject(kind+" icon",typeof(RectTransform)).AddComponent<ResourceIcon>();
            icon.transform.SetParent(parent,false); icon.Kind=kind; icon.raycastTarget=false; icon.rectTransform.sizeDelta=size; icon.SetVerticesDirty();
            return icon;
        }
    }
}
