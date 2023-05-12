using Elements;
using Elements.Geometry;
using System.Collections.Generic;

namespace ReceptionSignage
{
    public static class ReceptionSignage
    {
        /// <summary>
        /// The ReceptionSignage function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A ReceptionSignageOutputs instance containing computed results and the model with any new elements.</returns>
        public static ReceptionSignageOutputs Execute(Dictionary<string, Model> inputModels, ReceptionSignageInputs input)
        {
            var output = new ReceptionSignageOutputs();
            var inputFile = input.Signage.LocalFilePath == null ? null : input.Signage.LocalFilePath;
            var signageLength = input.Length;
            var signageHeight = input.DefaultHeight;
            var flip = input.Flip;

            var spacePlanningZones = inputModels["Space Planning Zones"];
            if (!File.Exists(inputFile))
            {
                output.Errors.Add("Input File was not received successfully.");
                return output;
            }

            var levels = spacePlanningZones.AllElementsOfType<LevelElements>();
            foreach (var lvl in levels)
            {
                var corridors = lvl.Elements.OfType<CirculationSegment>();
                var corridorSegments = corridors.SelectMany(p => p.Profile.Segments());
                var receptionRmBoundaries = lvl.Elements.OfType<SpaceBoundary>().Where(z => z.Name == "Reception");

                var hasCore = inputModels.TryGetValue("Core", out var coresModel) && coresModel.AllElementsOfType<ServiceCore>().Any();
                List<Line> coreSegments = new();
                if (coresModel != null)
                {
                    coreSegments.AddRange(coresModel.AllElementsOfType<ServiceCore>().SelectMany(c => c.Profile.Perimeter.Segments()));
                }

                var hasWalls = inputModels.TryGetValue("Walls", out var wallsModel) && wallsModel.AllElementsOfType<Wall>().Any();
                List<Line> wallSegments = new();
                if (wallsModel != null)
                {
                    wallSegments.AddRange(wallsModel.AllElementsOfType<Wall>().SelectMany(c => c.Profile.Perimeter.Segments()));
                }

                foreach (var room in receptionRmBoundaries)
                {
                    var spaceBoundary = room.Boundary;

                    Line orientationGuideEdge = hasCore ? FindEdgeClosestTo(spaceBoundary.Perimeter, coreSegments) : hasWalls ? FindEdgeClosestTo(spaceBoundary.Perimeter, wallSegments) : FindEdgeAdjacentToSegments(spaceBoundary.Perimeter.Segments(), corridorSegments, out var wallCandidates);
                    // offset image from edge to prevent plane overlapping
                    var moveVec = new Vector3((spaceBoundary.Perimeter.Centroid() - orientationGuideEdge.PointAt(0.5)).Unitized() * 0.01);
                    Line orientationGuideEdgeT = orientationGuideEdge.TransformedLine(new Transform(moveVec));

                    var imgRef = ModelImage.Create(inputFile, input, output, signageLength, signageHeight, orientationGuideEdgeT, flip);
                    if (imgRef == null)
                    {
                        return output;
                    }
                    output.Model.AddElement(imgRef);
                }
            }

            return output;
        }

        private static Line FindEdgeClosestTo(Polygon perimeter, List<Line> segments)
        {
            double dist = double.MaxValue;
            Line bestLine = null;

            foreach (var line in perimeter.Segments())
            {
                var lineMidPt = line.PointAt(0.5);
                var linePerp = line.Direction().Cross(Vector3.ZAxis).Unitized();
                foreach (var segment in segments)
                {
                    // don't consider perpendicular edges
                    if (Math.Abs(segment.Direction().Dot(line.Direction())) < 0.01)
                    {
                        continue;
                    }
                    var ptOnSegment = lineMidPt.ClosestPointOn(segment);
                    var thisDist = ptOnSegment.DistanceTo(lineMidPt);
                    if (thisDist < dist)
                    {
                        dist = thisDist;
                        bestLine = line;
                    }
                }

            }

            return bestLine;
        }

        private static Line FindEdgeAdjacentToSegments(IEnumerable<Line> edgesToClassify, IEnumerable<Line> corridorSegments, out IEnumerable<Line> otherSegments, double maxDist = 0)
        {
            var minDist = double.MaxValue;
            var minSeg = edgesToClassify.First();
            var allEdges = edgesToClassify.ToList();
            var selectedIndex = 0;
            for (int i = 0; i < allEdges.Count; i++)
            {
                var edge = allEdges[i];
                var midpt = edge.PointAt(0.5);
                foreach (var seg in corridorSegments)
                {
                    var dist = midpt.DistanceTo(seg);
                    // if two segments are basically the same distance to the corridor segment,
                    // prefer the longer one.
                    if (Math.Abs(dist - minDist) < 0.1)
                    {
                        minDist = dist;
                        if (minSeg.Length() < edge.Length())
                        {
                            minSeg = edge;
                            selectedIndex = i;
                        }
                    }
                    else if (dist < minDist)
                    {
                        minDist = dist;
                        minSeg = edge;
                        selectedIndex = i;
                    }
                }
            }
            if (maxDist != 0)
            {
                if (minDist < maxDist)
                {

                    otherSegments = Enumerable.Range(0, allEdges.Count).Except(new[] { selectedIndex }).Select(i => allEdges[i]);
                    return minSeg;
                }
                else
                {
                    Console.WriteLine($"no matches: {minDist}");
                    otherSegments = allEdges;
                    return null;
                }
            }
            otherSegments = Enumerable.Range(0, allEdges.Count).Except(new[] { selectedIndex }).Select(i => allEdges[i]);
            return minSeg;
        }
    }
}