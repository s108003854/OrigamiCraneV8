using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using GH_IO.Types;
using Rhino.UI;

namespace OrigamiCrane
{
    public class BuildModifyFurniture : GH_Component
    {

        private void AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel level, string message)
        {
            string timestamp = DateTime.Now.ToString("mm:ss.ffffff");
            string timestampedMessage = $"{timestamp}  |   {message}";
            AddRuntimeMessage(level, timestampedMessage);
        }

        /// <summary>
        /// 初始化 `BuildModifyFurniture` 類別的新實例。
        /// </summary>
        public BuildModifyFurniture()
          : base("BuildModifyFurniture", "B&M",
              "建構或修改家具部件",
              "OrigamiCrane", "Modify")
        {
        }

        /// <summary>
        /// 註冊組件的所有輸入參數。
        /// </summary>
        /// <param name="pManager">參數管理器</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // - 添加輸入參數
            //   * `Furniture JSON`: 家具的 JSON 數據
            //   * `Modify Part Name`: 要修改的部件名稱
            //   * `Curves Modify`: 修改曲線的數據
            //   * `Rotate Modify`: 旋轉修改數據
            //   * `Fillet Modify`: 圓角參數
            pManager.AddTextParameter("Furniture JSON", "JSON", "家具的 JSON 數據", GH_ParamAccess.item);
            pManager.AddTextParameter("Modify Part Name", "ModifyPart", "要修改的部件名稱", GH_ParamAccess.item);
            pManager.AddNumberParameter("Curves Modify", "CurvesModify", "修改曲線的數據", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Rotate Modify", "RotateModify", "旋轉修改數據", GH_ParamAccess.list);
            pManager.AddNumberParameter("Fillet Modify", "FilletModify", "圓角參數", GH_ParamAccess.tree);
        }

        /// <summary>
        /// 註冊組件的所有輸出參數。
        /// </summary>
        /// <param name="pManager">參數管理器</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // - 添加輸出參數
            //   * `Geo(Before)`: 修改前的 Brep 幾何
            //   * `Geo(After)`: 修改後的家具幾何
            pManager.AddGeometryParameter("Geo(Before)", "OrginalBrep", "修改前的 Brep 幾何", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Geo(After)", "Furniture", "修改後的家具幾何", GH_ParamAccess.list);
            pManager.AddCurveParameter("Json", "C", "修改後的曲線", GH_ParamAccess.list);
            pManager.AddTextParameter("log", "log", "運行日誌", GH_ParamAccess.list);
        }

        /// <summary>
        /// 此方法實際上處理邏輯。
        /// </summary>
        /// <param name="DA">DA 對象，用於從輸入中檢索並將結果存儲到輸出中。</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // === 宣告變數來存儲輸入數據 ===
            string furnitureJson = "";
            string modifyPartName = "";
            GH_Structure<GH_Number> curvesModify = new GH_Structure<GH_Number>();
            List<double> rotateModify = new List<double>();
            GH_Structure<GH_Number> filletModify = new GH_Structure<GH_Number>();
            List<string> new_furnitureJson = new List<string>();

            // === 檢索輸入數據 ===
            if (!DA.GetData(0, ref furnitureJson)) return;  // 獲取 `Furniture JSON`
            DA.GetData(1, ref modifyPartName);  // 獲取 `Modify Part Name`
            DA.GetDataTree(2, out curvesModify);  // 獲取 `Curves Modify`
            DA.GetDataList(3, rotateModify);  // 獲取 `Rotate Modify`
            DA.GetDataTree(4, out filletModify);  // 獲取 `Fillet Modify`

            // === 解析 JSON 數據 ===
            JArray dataJson = JArray.Parse(furnitureJson);  // 解析 JSON 數據為 JArray

            // === 主處理邏輯 ===
            List<Brep> furniture_After = new List<Brep>();  // 存儲修改後的家具
            List<Brep> furniture_Before = new List<Brep>();  // 存儲修改前的家具
            List<Curve> modifiedCurves = new List<Curve>();  // 存儲修改後的曲線
            Plane localPlane = Plane.Unset;  // 局部平面

            // === 遍歷所有家具部件並進行處理 ===
            foreach (JObject part in dataJson)
            {
                string partName = part["name"].ToString();  // 取得部件名稱
                bool toggleModify = partName == modifyPartName;  // 判斷是否是要修改的部件

                // 根據部件是否需要修改來記錄運行信息
                if (!toggleModify)
                {
                    AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "跳過部件: " + partName);

                }
                else
                {
                    AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "-------------開始處理部件-------------");
                    AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "正在修改部件: " + partName);
                }

                // --- 獲取部件的 JSON 數據 ---
                JObject curveJson = (JObject)part["curveParameters"];  // 曲線參數
                JObject rotateJson = (JObject)part["rotateParameters"];  // 旋轉參數
                JObject filletJson = (JObject)part["filletParameters"];  // 圓角參數
                JObject localPlaneJson = (JObject)part["localPlane"];  // 局部平面數據

                // --- 創建幾何 ---
                localPlane = CreateLocalPlane(localPlaneJson, toggleModify);  // 創建局部平面
                List<Curve> curves = CreateStructureCurvesFromJson(curveJson, toggleModify);  // 根據 JSON 創建曲線

                Brep brep_before = CreateBrepFromCurves(curves, curveJson, toggleModify);  // 創建修改前的 Brep
                furniture_Before.Add(brep_before);  // 添加到修改前的家具列表

                modifiedCurves = ModifyCurves(curves, curvesModify, localPlane, toggleModify);  // 修改曲線
                Brep brep = CreateBrepFromCurves(modifiedCurves, curveJson, toggleModify);  // 根據修改後的曲線創建 Brep
                furniture_After.Add(brep);  // 添加到修改後的家具列表

                //------------------------------------------------------------------------

                // 修改旋轉數據
                JObject modifiedRotateJson = ModifyRotate(rotateJson, rotateModify, toggleModify);
                // 根據修改後的旋轉數據旋轉 Brep
                Brep rotatedBrep = RotateBrep(brep, modifiedRotateJson, localPlane, toggleModify);

                // 修改圓角數據
                JObject modifiedFilletJson = ModifyFilletJson(filletJson, filletModify, toggleModify);
                // 把 JSON 資料中的 edgeIndex 改成用對應的邊線中點表示
                JObject MidPtsFilletJson = UpdateFilletJsonWithMidpoints(rotatedBrep, modifiedFilletJson, toggleModify);
                // 根據新的圓角數據進行圓角處理
                Brep filletedBrep = FilletBrep(rotatedBrep, MidPtsFilletJson, toggleModify);
                // 添加到修改後的家具列表
                furniture_After.Add(filletedBrep);

                // 記錄處理完成的信息
                if (toggleModify)
                {
                    AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "-------------部件處理完成-------------");
                }
            }

            // === 設置輸出數據 ===
            List<string> runtimeMessages = RuntimeMessages(GH_RuntimeMessageLevel.Remark).ToList();


            DA.SetDataList("Geo(Before)", furniture_Before);  // 輸出修改後的家具
            DA.SetDataList("Geo(After)", furniture_After);  // 輸出修改後的曲線
            DA.SetDataList("Json", new_furnitureJson);  // 輸出修改後的 JSON
            DA.SetDataList("log", runtimeMessages);  // 輸出運行日誌
        }

        /// <summary>
        /// 提供組件的圖標。
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // 也可以將圖像文件添加到項目資源中，然後像這樣訪問它們：
                // return Resources.IconForThisComponent;
                return null;  // 此處暫不提供圖標
            }
        }

        /// <summary>
        /// 獲取組件的唯一ID。發布後不要更改此ID。
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("C10FB244-DD49-4F8F-B2ED-74E109E07BF0"); }  // 返回唯一的組件ID
        }


        //// =============================================================
        //// ===================== 定義的所有方法在這裡 =====================
        //// =============================================================
        /// 從 JSON 數據創建結構曲線。
        private List<Curve> CreateStructureCurvesFromJson(JObject curveJson, bool toggleModify)
        {
            // - 創建曲線列表
            List<Curve> curves = new List<Curve>();

            // - 取得曲線1的點集
            double[][] curve1PtSet = curveJson["curve1PointSet"].ToObject<double[][]>();
            // - 取得曲線2的點集
            double[][] curve2PtSet = curveJson["curve2PointSet"].ToObject<double[][]>();

            // - 將曲線1的點集轉換為 Point3d
            List<Point3d> ptset1 = curve1PtSet.Select(coords => new Point3d(coords[0], coords[1], coords[2])).ToList();
            // - 根據點集創建插值曲線，曲線階數為3，節點樣式為均勻
            curves.Add(Curve.CreateInterpolatedCurve(ptset1, 3, CurveKnotStyle.Uniform));

            // - 將曲線2的點集轉換為 Point3d
            List<Point3d> ptset2 = curve2PtSet.Select(coords => new Point3d(coords[0], coords[1], coords[2])).ToList();
            // - 根據點集創建插值曲線，曲線階數為3，節點樣式為均勻
            curves.Add(Curve.CreateInterpolatedCurve(ptset2, 3, CurveKnotStyle.Uniform));

            // - 返回曲線列表
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Curve Created");
            }
            return curves;
        }

        /// 從 JSON 數據創建局部平面。
        private Plane CreateLocalPlane(JObject localPlaneJson, bool toggleModify)
        {
            // - 取得平面原點坐標
            Point3d originPt = new Point3d(
                (double)localPlaneJson["originX"],
                (double)localPlaneJson["originY"],
                (double)localPlaneJson["originZ"]
            );

            // - 取得平面法向量
            Vector3d normal = new Vector3d(
                (double)localPlaneJson["normalX"],
                (double)localPlaneJson["normalY"],
                (double)localPlaneJson["normalZ"]
            );

            // - 創建並返回局部平面
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Plane Created");
            }
            return new Plane(originPt, normal);
        }

        /// 根據曲線和 JSON 數據創建 Brep。
        private Brep CreateBrepFromCurves(List<Curve> curves, JObject curveJson, bool toggleModify)
        {
            // - 取得厚度參數
            double thickness = curveJson["thickness"].Value<double>();
            Curve curve1 = curves[0];  // 曲線1
            Curve curve2 = curves[1];  // 曲線2

            // - 使用 SweepOneRail 創建掃略曲面
            SweepOneRail sweepOneRail = new SweepOneRail();
            Brep[] sweep = sweepOneRail.PerformSweep(curve1, curve2);

            // - 將掃略曲面進行偏移以創建Brep
            Brep[] offsetResult = Brep.CreateOffsetBrep(sweep[0], thickness, true, true, 0.1, out _, out _);

            // - 返回生成的 Brep
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Brep Created");
            }
            return offsetResult[0];
        }

        /// 根據修改數據和局部平面來修改曲線
        private List<Curve> ModifyCurves(List<Curve> curves, GH_Structure<GH_Number> curvesModify, Plane localPlane, bool toggleModify)
        {
            // --- 檢查是否需要進行曲線修改 ---
            // 如果曲線修改數據為空，或不需要進行修改，則直接返回原始曲線列表
            if (curvesModify.PathCount == 0 || !toggleModify)
            {
                return curves;  // 不進行任何修改，返回原曲線
            }

            // --- 進行曲線修改的邏輯 ---
            // 獲取曲線1的移動數據，這些數據將指示每個分割點需要沿著局部平面的Z軸移動的距離
            List<double> crv1move = curvesModify.Branches[0].Select(n => n.Value).ToList();

            // 獲取曲線2的移動數據，與曲線1類似
            List<double> crv2move = curvesModify.Branches[1].Select(n => n.Value).ToList();

            // 從修改數據中取得曲線分割的數量，即將曲線分成多少段
            int divCount1 = crv1move.Count - 1;
            int divCount2 = crv2move.Count - 1;

            // 初始化一個新的曲線列表，用來存儲修改後的曲線
            List<Curve> newCurves = new List<Curve>();

            // --- 修改每條曲線 ---
            // 循環遍歷兩條曲線（curves[0] 和 curves[1]）
            for (int i = 0; i < 2; i++)
            {
                // 當前處理的曲線
                Curve crv = curves[i];

                // 獲取對應的移動數據，如果是第一條曲線使用 crv1move，第二條使用 crv2move
                List<double> moveValues = i == 0 ? crv1move : crv2move;

                // 獲取對應的分段數量，如果是第一條曲線使用 crv1move，第二條使用 crv2move
                int divCount = i == 0 ? divCount1 : divCount2;

                // 將曲線分割成若干段，返回每段的參數值（即曲線上的位置）
                double[] parameters = crv.DivideByCount(divCount, true);

                // 初始化一個新的點集，用來存儲修改後的點
                List<Point3d> newPoints = new List<Point3d>();

                // --- 修改每個分割點的位置 ---
                // 遍歷每個分割點，根據移動數據對其進行移動
                for (int j = 0; j < parameters.Length; j++)
                {
                    // 獲取當前分割點在曲線上的位置
                    Point3d pt = crv.PointAt(parameters[j]);

                    // 設定移動方向為局部平面的Z軸方向
                    Vector3d moveDirection = localPlane.ZAxis;

                    // 將方向向量標準化為單位向量
                    moveDirection.Unitize();

                    // 根據移動數據（沿Z軸方向的距離）來計算最終的移動向量
                    moveDirection *= moveValues[j];

                    // 將點沿著計算出的移動向量進行平移
                    pt += moveDirection;

                    // 將移動後的點添加到新的點集中
                    newPoints.Add(pt);
                }

                // --- 根據新的點集生成新的曲線 ---
                // 使用修改後的點集創建插值曲線，該曲線與原曲線具有相同的階數（3）和節點樣式（均勻）
                newCurves.Add(Curve.CreateInterpolatedCurve(newPoints, 3, CurveKnotStyle.Uniform));
            }

            // --- 返回修改後的曲線 ---
            // 返回包含所有修改後曲線的列表
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "--修改curve參數");
            }

            return newCurves;
        }


        /// 根據用戶的旋轉修改值，更新旋轉的 JSON 對象。
        private JObject ModifyRotate(JObject rotateJson, List<double> rotateModify, bool toggleModify)
        {
            // 若沒有旋轉修改值或修改功能被禁用，直接返回原始的 JSON 對象。
            if (rotateModify.Count == 0 || !toggleModify)
            {
                return rotateJson;
            }

            // 更新 JSON 對象中的旋轉角度值。
            rotateJson["rotateDegrees"]["rx"] = rotateModify[0];
            rotateJson["rotateDegrees"]["ry"] = rotateModify[1];
            rotateJson["rotateDegrees"]["rz"] = rotateModify[2];

            // 增加運行時訊息，提示用戶已經修改了旋轉 JSON。
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "--修改rotate參數");
            }
            return rotateJson;
        }


        /// 根據提供的 JSON 旋轉參數對 Brep 進行旋轉。
        private Brep RotateBrep(Brep brep, JObject rotateJson, Plane localPlane, bool toggleModify)
        {
            // 從 JSON 對象中提取旋轉角度值。
            double rx = (double)rotateJson["rotateDegrees"]["rx"];
            double ry = (double)rotateJson["rotateDegrees"]["ry"];
            double rz = (double)rotateJson["rotateDegrees"]["rz"];

            // 從 JSON 對象中提取是否使用本地平面的標誌。
            bool useLocalPlane = (bool)rotateJson["useLocalPlane"];

            // 獲取 Brep 的包圍盒中心點。
            Point3d center = brep.GetBoundingBox(true).Center;

            // 定義中心平面和旋轉平面。
            Plane centerPL = new Plane(center, Vector3d.ZAxis);
            Plane rotatePlane = useLocalPlane ? localPlane : centerPL;

            // 計算旋轉變換矩陣。
            Transform tx = Transform.Rotation(RhinoMath.ToRadians(rx), rotatePlane.XAxis, center) *
                            Transform.Rotation(RhinoMath.ToRadians(ry), rotatePlane.YAxis, center) *
                            Transform.Rotation(RhinoMath.ToRadians(rz), rotatePlane.ZAxis, center);

            // 複製 Brep 並應用旋轉變換。
            Brep rotatedBrep = brep.DuplicateBrep();
            rotatedBrep.Transform(tx);

            // 增加運行時訊息，提示 Brep 已經進行旋轉。
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Brep Rotated");
            }
            return rotatedBrep;
        }

        /// 根據用戶提供的修改數據，更新 fillet 的 JSON 對象。

        private JObject ModifyFilletJson(JObject filletJson, GH_Structure<GH_Number> modifyTree, bool toggleModify)
        {
            // 遍歷 modifyTree 的每個路徑
            foreach (GH_Path path in modifyTree.Paths)
            {
                // 取得當前路徑下的所有數值
                List<GH_Number> modifyPair = modifyTree[path];

                // 確保 modifyPair 至少包含三個元素
                if (modifyPair.Count != 3)
                {
                    throw new ArgumentException("Invalid modifyPair. It should contain 3 elements.");
                }

                // 根據 modifyPair[2] 的值選擇正確的 Fillet 類型
                JArray edgeIndex;
                JArray edgeRadii;

                switch ((double)modifyPair[2].Value)
                {
                    case 0:
                        edgeIndex = (JArray)filletJson["ChamferFillet"]["edgeIndex"];
                        edgeRadii = (JArray)filletJson["ChamferFillet"]["edgeRadii"];
                        break;
                    case 1:
                        edgeIndex = (JArray)filletJson["FilletFillet"]["edgeIndex"];
                        edgeRadii = (JArray)filletJson["FilletFillet"]["edgeRadii"];
                        break;
                    case 2:
                        edgeIndex = (JArray)filletJson["BlendFillet"]["edgeIndex"];
                        edgeRadii = (JArray)filletJson["BlendFillet"]["edgeRadii"];
                        break;
                    default:
                        throw new ArgumentException("Invalid value in modifyPair[2]");
                }

                // 從 modifyPair 中提取數值
                int edgeIndexValue = (int)modifyPair[0].Value;  // 要修改或添加的邊緣索引
                double edgeRadiiValue = (double)modifyPair[1].Value;  // 要修改或添加的邊緣半徑

                // 查找 edgeIndex 中是否已經存在要修改的邊緣索引
                int indexToUpdate = edgeIndex.IndexOf(edgeIndexValue);

                if (indexToUpdate != -1)
                {
                    // 如果找到匹配的邊緣索引
                    if (edgeRadiiValue < 0)
                    {
                        // 如果 edgeRadiiValue 小於 0，則刪除對應的 edgeIndex 和 edgeRadii
                        edgeIndex.RemoveAt(indexToUpdate);
                        edgeRadii.RemoveAt(indexToUpdate);
                    }
                    else
                    {
                        // 否則，更新對應的 edgeRadii
                        edgeRadii[indexToUpdate] = edgeRadiiValue;
                    }
                }
                else
                {
                    // 如果沒有找到匹配的邊緣索引，則添加新的邊緣索引和半徑到末尾
                    edgeIndex.Add(edgeIndexValue);
                    edgeRadii.Add(edgeRadiiValue);
                }
            }

            // 增加運行時訊息，提示用戶已經修改了 fillet JSON
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "--修改fillet參數");
            }
            return filletJson;
        }

        /// 根據邊緣的中點位置更新 fillet 的 JSON 對象。

        private JObject UpdateFilletJsonWithMidpoints(Brep geo, JObject filletJson, bool toggleModify)
        {
            // 獲取所有邊緣的中點
            List<Point3d> allMidpts = geo.Edges.Select(edge => edge.PointAt(0.5)).ToList();

            // 定義需要處理的 fillet 類型
            string[] filletTypes = { "ChamferFillet", "FilletFillet", "BlendFillet" };

            // 遍歷 filletTypes 中的每種類型
            foreach (string filletType in filletTypes)
            {
                // 提取對應的 edgeIndex 和 edgeRadii
                JArray edgeIndexArray = (JArray)filletJson[filletType]["edgeIndex"];
                JArray edgeRadiiArray = (JArray)filletJson[filletType]["edgeRadii"];

                // 創建一個新的 JArray 來存儲中點位置
                JArray updatedMidpointsArray = new JArray();

                // 遍歷 edgeIndexArray 中的每個索引
                foreach (JToken edgeIndexToken in edgeIndexArray)
                {
                    int edgeIndexValue = edgeIndexToken.Value<int>();

                    // 確保 edgeIndexValue 在範圍內
                    if (edgeIndexValue >= 0 && edgeIndexValue < allMidpts.Count)
                    {
                        // 獲取對應的中點
                        Point3d midpoint = allMidpts[edgeIndexValue];
                        // 創建一個新的 JArray 來表示中點的座標
                        JArray midpointArray = new JArray(midpoint.X, midpoint.Y, midpoint.Z);
                        // 添加到 updatedMidpointsArray 中
                        updatedMidpointsArray.Add(midpointArray);
                    }
                }

                // 更新 filletJson 的 edgeIndex 為中點位置
                JObject filletTypeObj = (JObject)filletJson[filletType];
                filletTypeObj["edgeMidPt"] = updatedMidpointsArray;
                // 刪除原有的 edgeIndex
                filletTypeObj.Remove("edgeIndex");
            }

            // 增加運行時訊息，提示 fillet JSON 已經更新
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "--把邊界編號替換成實際幾何點");
            }
            return filletJson;
        }

        /// 根據 fillet 的 JSON 配置對 Brep 進行圓角處理。
        private Brep FilletBrep(Brep brep, JObject filletJson, bool toggleModify)
        {
            // 如果 filletJson 為空或圓角修改被禁用，直接返回原始的 Brep 對象
            if (filletJson.Count == 0 || !toggleModify)
            {
                return brep;
            }

            Brep newBrep = brep;
            BlendType blendType = BlendType.Fillet;

            // 定義需要處理的 fillet 類型
            string[] filletTypes = { "ChamferFillet", "FilletFillet", "BlendFillet" };

            // 遍歷 filletTypes 中的每種類型
            foreach (string filletType in filletTypes)
            {
                JObject filletTypeDict = (JObject)filletJson[filletType];

                // 從 JSON 中提取 edgeMidPt 和 edgeRadii
                JArray edgeMidPtArray = (JArray)filletTypeDict["edgeMidPt"];
                JArray edgeRadiiArray = (JArray)filletTypeDict["edgeRadii"];

                // 解析 edgeMidPt 為 Point3d 的列表
                List<Point3d> filletMidPts = edgeMidPtArray.Select(ptArray =>
                    new Point3d(ptArray[0].Value<double>(), ptArray[1].Value<double>(), ptArray[2].Value<double>())
                ).ToList();

                // 解析 edgeRadii 為 double[]
                double[] filletRadii = edgeRadiiArray.ToObject<double[]>();

                // 根據 filletType 決定 BlendType
                if (filletType == "ChamferFillet") { blendType = BlendType.Chamfer; }
                if (filletType == "FilletFillet") { blendType = BlendType.Fillet; }
                if (filletType == "BlendFillet") { blendType = BlendType.Blend; }

                // 找出最接近的邊索引
                List<Point3d> allMidpts = newBrep.Edges.Select(edge => edge.PointAt(0.5)).ToList();
                int[] closestIndices = filletMidPts.Select(ptB =>
                    allMidpts.Select((ptA, index) => new { Index = index, Distance = ptA.DistanceTo(ptB) })
                          .OrderBy(x => x.Distance)
                          .First().Index
                ).ToArray();

                // 使用 Brep.CreateFilletEdges 創建圓角
                Brep[] filletedBrep = Brep.CreateFilletEdges(newBrep, closestIndices, filletRadii, filletRadii, blendType, RailType.RollingBall, 0.1);

                // 如果成功，更新 Brep
                if (filletedBrep != null && filletedBrep.Length > 0)
                {
                    newBrep = filletedBrep[0];
                }
            }

            // 增加運行時訊息，提示 Brep 已經進行圓角處理
            if (toggleModify)
            {
                AddTimestampedRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Brep Filleted");
            }

            return newBrep;
        }


    }
}
