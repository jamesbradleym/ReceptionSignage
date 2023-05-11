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
        }

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

            var widthRescale = width / height;

            var Mesh = new Mesh();

            var vertTransform = new Transform();
            // // Scale image mesh to correct w/h ratio
            vertTransform.Scale(new Vector3(widthRescale, 1, 1));
            // // Scale image mesh to target dimension
            vertTransform.Scale(targetWidth / widthRescale);

            // Transform the vertex points to scale
            var vertices = new List<Vertex>() {
                new Vertex(vertTransform.OfPoint(new Vector3(-0.5,-0.5)), Vector3.ZAxis,Colors.White,0,new UV(0,0)),
                new Vertex(vertTransform.OfPoint(new Vector3(-0.5,0.5)), Vector3.ZAxis,Colors.White,0,new UV(0,1)),
                new Vertex(vertTransform.OfPoint(new Vector3(0.5,0.5)), Vector3.ZAxis,Colors.White,0,new UV(1, 1)),
                new Vertex(vertTransform.OfPoint(new Vector3(0.5,-0.5)), Vector3.ZAxis,Colors.White,0,new UV(1,0)),
            };
            vertices.ForEach(v => Mesh.AddVertex(v));
            Mesh.AddTriangle(vertices[0], vertices[1], vertices[2]);
            Mesh.AddTriangle(vertices[2], vertices[3], vertices[0]);

            var color = new Elements.Geometry.Color(1, 1, 1, 1);
            var Material = new Material(path, color, 0, 0, path, true, true);

            var transform = new Transform();

            // Flip over Y Axis at user preference
            transform.Rotate(Vector3.YAxis, flip ? 180.0f : 0.0f);
            // Rotate image mesh around Z-Axis to line up with best edge
            float angle = (float)Math.Atan2(bestEdge.Direction().Y, bestEdge.Direction().X) - (float)Math.Atan2(Vector3.XAxis.Y, Vector3.XAxis.X);
            transform.Rotate(Vector3.ZAxis, angle * (180.0f / (float)Math.PI));
            // Rotate image mesh around best Edge so image is vertical
            transform.Rotate(bestEdge.Direction(), 90.0f);
            // Move image mesh to the midpoint of bestedge
            transform.Move(bestEdge.PointAtNormalized(0.5));
            // Move image mesh to user preference Z
            transform.Move(new Vector3(0, 0, targetZ));

            var imgRef = new ModelImage(Mesh, Material, transform, path);

            if (input.Overrides != null && input.Overrides.Position.Count > 0 && input.Overrides.Position != null)
            {
                var overrideVal = input.Overrides.Position.First();
                Identity.AddOverrideIdentity(imgRef, "Position", overrideVal.Id, overrideVal.Identity);

                imgRef.Transform = overrideVal.Value.Transform;
            }

            return imgRef;
        }
    }
}