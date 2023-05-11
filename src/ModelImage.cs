using System;
using System.Collections.Generic;
using Elements.Geometry;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using Newtonsoft.Json;
using Elements.Annotations;
using ReceptionSignage;
using System.Linq;

namespace Elements
{
    public class ModelImage : MeshElement
    {
        private ModelImage(Mesh mesh, Material mat, Transform xform, string path) : base(mesh, xform, mat)
        {
            this.ImagePath = path;
            this.PositionTransform = new Transform();
        }

        [JsonProperty("Position Transform")]
        public Transform PositionTransform { get; set; }

        [JsonProperty("Image Path")]
        public string ImagePath { get; set; }

        public static ModelImage Create(
            string path,
            ReceptionSignageInputs input,
            ReceptionSignageOutputs output,
            double targetWidth,
            double targetZ,
            Line bestEdge,
            bool flip = false
            )
        {
            Image image;
            image = Image.Load(path);
            var bounds = image.Bounds();
            // Console.WriteLine(bounds);
            var width = (double)bounds.Width;
            var height = (double)bounds.Height;

            var Mesh = new Mesh();
            var vertices = new List<Vertex>() {
                new Vertex(new Vector3(0,0), Vector3.ZAxis,Colors.White,0,new UV(0,0)),
                new Vertex(new Vector3(0,1), Vector3.ZAxis,Colors.White,0,new UV(0,1)),
                new Vertex(new Vector3(1,1), Vector3.ZAxis,Colors.White,0,new UV(1, 1)),
                new Vertex(new Vector3(1,0), Vector3.ZAxis,Colors.White,0,new UV(1,0)),
            };
            vertices.ForEach(v => Mesh.AddVertex(v));
            Mesh.AddTriangle(vertices[0], vertices[1], vertices[2]);
            Mesh.AddTriangle(vertices[2], vertices[3], vertices[0]);

            var color = new Elements.Geometry.Color(1, 1, 1, 1);
            var Material = new Material(path, color, 0, 0, path, true, true);

            var widthRescale = width / height;
            var transform = new Transform();
            transform.Scale(new Vector3(widthRescale, 1, 1));
            transform.Scale(targetWidth / widthRescale);
            transform.Move(-targetWidth / 2);
            transform.Rotate(Vector3.YAxis, flip ? 180.0f : 0.0f);
            transform.Rotate(Vector3.ZAxis, (float)Math.Acos(bestEdge.Direction().Dot(Vector3.XAxis)) * (180.0f / (float)Math.PI));
            transform.Rotate(bestEdge.Direction(), 90.0f);
            transform.Move(bestEdge.PointAt(0.5));
            transform.Move(new Vector3(0, 0, targetZ));

            var imgRef = new ModelImage(Mesh, Material, transform, path);

            if (input.Overrides != null && input.Overrides.Position.Count > 0 && input.Overrides.Position != null)
            {
                var overrideVal = input.Overrides.Position.First();
                imgRef.Transform.Concatenate(overrideVal.Value.PositionTransform);
                Identity.AddOverrideIdentity(imgRef, "Position", overrideVal.Id, overrideVal.Identity);
                imgRef.PositionTransform = overrideVal.Value.PositionTransform;
            }

            return imgRef;
        }
    }
}