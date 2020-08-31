using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AntDiary
{
    /// <summary>
    /// 巣統合システム。
    /// シングルトンで動くので、NestSystem.Instanceでシングルトンインスタンスにアクセス可能。
    /// </summary>
    public class NestSystem : MonoBehaviour, IDebugMenu
    {
        public NestData Data => GameContext.Current?.s_NestData;

        #region Singleton Implementation

        public static NestSystem Instance { get; private set; }

        /// <summary>
        /// 自身をSingletonのインスタンスとして登録。既に別のインスタンスが存在する場合はfalseを返す。
        /// </summary>
        /// <returns></returns>
        private bool RegisterSingletonInstance()
        {
            if (!Instance)
            {
                Instance = this;
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        private void Awake()
        {
            if (!RegisterSingletonInstance())
            {
                Destroy(gameObject);
                return;
            }
        }

        [SerializeField] private AntFactory[] antFactories = default;
        [SerializeField] private NestElementFactory[] nestElementFactories = default;

        public BuildingSystem BuildingSystem { get; set; }

        private readonly List<Ant> spawnedAnts = new List<Ant>();
        
        /// <summary>
        /// 巣に存在するすべてのアリを取得する。
        /// </summary>
        public IReadOnlyList<Ant> SpawnedAnt => spawnedAnts;

        private readonly List<NestElement> nestElements = new List<NestElement>();
        
        /// <summary>
        /// 巣に存在するすべてのNestElementを取得する。
        /// </summary>
        public IReadOnlyList<NestElement> NestElements => nestElements;

        private readonly List<NestPathElementEdge> elementEdges = new List<NestPathElementEdge>();
        
        /// <summary>
        /// NestElement間の接続をすべて取得する。
        /// </summary>
        public IReadOnlyList<NestPathElementEdge> ElementEdges => elementEdges;

        /// <summary>
        /// 建築中のNestElementをすべて取得する。
        /// </summary>
        public IEnumerable<NestBuildableElement> BuildingElements =>
            nestElements.OfType<NestBuildableElement>().Where(d => d.IsUnderConstruction);

        /// <summary>
        /// 巣に存在するすべてのIPathNodeを取得する。
        /// </summary>
        public IEnumerable<NestPathNode> NestPathNodes => NestElements.SelectMany(e => e.GetNodes());

        
        private readonly Dictionary<(NestPathNode from, NestPathNode to), IEnumerable<IPathNode>> _routeSearchCache = new Dictionary<(NestPathNode, NestPathNode), IEnumerable<IPathNode>>();

        
        private void Start()
        {
            if (SaveUnit.Current != null)
            {
                //すでにロード済み
                LoadData();
            }

            //次にセーブデータが変更（ロード）されたときに、巣を更新する
            SaveUnit.OnCurrentSaveUnitChanged.Subscribe(su => LoadData());

            
            this.ObserveEveryValueChanged(system => system.nestElements)
                .Subscribe(list => {
                    OnChangeNestPath();
                });
            
            BuildingSystem = new BuildingSystem(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnChangeNestPath(){
            Debug.Log("onNestElementsChanged");
            _routeSearchCache.Clear();
        }
        

        /// <summary>
        /// NestDataをもとに巣を再構築する。
        /// </summary>
        private void LoadData()
        {
            //生成済みのアリを破棄
            foreach (var ant in spawnedAnts)
            {
                Destroy(ant.gameObject);
            }

            spawnedAnts.Clear();

            //生成済みの部屋や道を破棄
            foreach (var element in nestElements)
            {
                Destroy(element.gameObject);
            }

            nestElements.Clear();

            elementEdges.Clear();


            //セーブデータから巣情報をロード

            //アリを生成
            foreach (var antData in Data.Ants)
            {
                var ant = InstantiateAnt(antData, false);
            }

            //道、部屋を生成
            foreach (var elementData in Data.Structure.NestElements)
            {
                var element = InstantiateNestElement(elementData, false);
            }

            //Element間の接続をロード
            foreach (var edgeData in Data.Structure.ElementEdges)
            {
                var edge = new NestPathElementEdge(nestElements, edgeData);
                elementEdges.Add(edge);
            }
        }


        /// <summary>
        /// AntDataをもとに、対応するAntFactoryを用いてAntのインスタンスを生成する。
        /// </summary>
        /// <param name="antData">生成に使用するAntData。</param>
        /// <param name="registerToGameContext">新たにGameContextに登録するかどうか。セーブデータからの生成などの際に限りfalseを指定する。</param>
        /// <returns>生成されたGameObjectのもつAntコンポーネント。</returns>
        public Ant InstantiateAnt(AntData antData, bool registerToGameContext = true)
        {
            //Debug.Log(antData.GetType());
            var ant = antFactories.FirstOrDefault(f => f.DataType == antData.GetType())?.InstantiateAnt(antData);
            if (ant != null)
            {
                if (registerToGameContext)
                {
                    Data.Ants.Add(antData);
                }

                spawnedAnts.Add(ant);
            }

            return ant;
        }

        /// <summary>
        /// InstantiateAntで生成したアリを破棄します。
        /// セーブデータから該当のアリを削除します。
        /// GameObjectの破棄までは担当しないので、呼び出し側でDestroyしてください。
        /// </summary>
        /// <param name="ant"></param>
        public void RemoveAnt(Ant ant)
        {
            if (Data.Ants.Contains(ant.Data))
            {
                Data.Ants.Remove(ant.Data);
            }
        }


        /// <summary>
        /// NestElementDataをもとに、対応するNestElementFactoryを用いてNestElementのインスタンスを生成する。
        /// </summary>
        /// <param name="elementData">生成に使用するNestElementData。</param>
        /// <param name="registerToGameContext">新たにGameContextに登録するかどうか。セーブデータからの生成などの際に限りfalseを指定する。</param>
        /// <returns>生成されたGameObjectのもつNestElementコンポーネント。</returns>
        public NestElement InstantiateNestElement(NestElementData elementData, bool registerToGameContext = true)
        {

            NestElementFactory matchedFactory = null;
            foreach (var f in nestElementFactories)
            {
                if (f.DataType == elementData.GetType())
                {
                    matchedFactory = f;
                    break;
                }else if (matchedFactory == null && elementData.GetType().IsSubclassOf(f.DataType))
                {
                    matchedFactory = f;
                }
            }
            
            if(!matchedFactory) Debug.LogWarning($"NestSystem: {elementData.GetType()} 用のNestElementFactoryは登録されていません。");
            
            var nestElement = matchedFactory?.InstantiateNestElement(elementData);
            
            if (nestElement != null)
            {
                if (registerToGameContext)
                {
                    Data.Structure.NestElements.Add(elementData);
                }

                nestElements.Add(nestElement);
            }

            return nestElement;
        }

        /// <summary>
        /// InstantiateNestElement()で追加したNestElementを削除します。
        /// セーブデータからNestElementを削除し、関連するElement間接続を破棄します。
        /// GameObjectの破棄までは担当しないので、呼び出し側でDestroyしてください
        /// </summary>
        /// <param name="element"></param>
        public void RemoveNestElement(NestElement element)
        {
            if (Data.Structure.NestElements.Contains(element.Data))
            {
                Data.Structure.NestElements.Remove(element.Data);
                nestElements.Remove(element);
                //Element間接続がある場合はその接続も破棄
                var l = elementEdges.Where(e => e.A.Host == element || e.B.Host == element).ToList();
                foreach (var e in l)
                {
                    if (Data.Structure.ElementEdges.Contains(e.Data))
                    {
                        Data.Structure.ElementEdges.Remove(e.Data);
                    }

                    e.Clear();
                    elementEdges.Remove(e);
                }
            }
        }

        /// <summary>
        /// 外部で生成したNestElementをNestSystemに登録する。
        /// あらかじめNestElement.Initializeで適切なNestElementDataが注入されている必要がある。
        /// </summary>
        /// <param name="element"></param>
        public void RegisterNestElementToGameContext(NestElement element)
        {
            if (element.Data == null)
                throw new ArgumentException("NestElementDataが設定されていません。あらかじめ適切なNestElementDataを注入してください。");
            if (Data.Structure.NestElements.Contains(element.Data))
                throw new ArgumentException("指定されたNestElementのDataはすでに登録されています。");
            
            if (!nestElements.Contains(element)) nestElements.Add(element);
            
            Data.Structure.NestElements.Add(element.Data);
        }

        public NestPathElementEdge ConnectElements(NestPathNode a, NestPathNode b)
        {
            var edge = new NestPathElementEdge(a, b);

            elementEdges.Add(edge);
            Data.Structure.ElementEdges.Add(edge.Data);
            return edge;
        }


        
        public IEnumerable<IPathNode> FindRoute(NestPathNode from, NestPathNode to)
        {
            
            if(_routeSearchCache.ContainsKey((from, to))){
                Debug.Log("use cache");
                return _routeSearchCache[(from, to)];
            }
            
            AStarSearcher searcher = new AStarSearcher(null);
            
            searcher.SearchRoute(from, to);
            _routeSearchCache[(from, to)] = searcher.Route;
            
            return searcher.Route;
        }


        #region Debug

        public string pageTitle { get; } = "巣統合システム";
        private bool showGraph = true;
        private IEnumerable<IPathNode> latestRoute;

        public void OnGUIPage()
        {
            GUILayout.Label($"データのロード: {(Data != null ? "済" : "未")}");
            if (Data != null)
            {
                GUILayout.Label($"SpawnedAnts: {spawnedAnts.Count}");
                GUILayout.Label($"NestElements: {nestElements.Count}");
                if (GUILayout.Button("デバッグアリのスポーン"))
                {
                    InstantiateAnt(new DebugAntData());
                }

                if (GUILayout.Button("デバッグ巣の生成"))
                {
                    //生成済みの巣のクリア
                    var ne = nestElements.ToList();
                    foreach (var e in ne)
                    {
                        RemoveNestElement(e);
                    }

                    for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        float posx = x * 4f - 1.5f * 4f;
                        float posy = y * 3f - 1f * 3f;
                        InstantiateNestElement(new DebugRoomData()
                            {Position = new Vector2(posx, posy), IsUnderConstruction = false});
                    }

                    for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        int i = y * 4 + x;
                        var n1 = NestElements[i].GetNodes().First(n => n.Name == "right");
                        var n2 = NestElements[i + 1].GetNodes().First(n => n.Name == "left");

                        var road = InstantiateNestElement(new DebugRoadData()
                            {From = n1.WorldPosition, To = n2.WorldPosition, IsUnderConstruction = false});
                        var roadNodes = road.GetNodes();
                        var r1 = roadNodes.ElementAt(0);
                        var r2 = roadNodes.ElementAt(1);

                        ConnectElements(n1, r1);
                        ConnectElements(r2, n2);
                    }


                    for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int i = y * 4 + x;
                        var n1 = NestElements[i].GetNodes().First(n => n.Name == "top");
                        var n2 = NestElements[i + 4].GetNodes().First(n => n.Name == "bottom");

                        var road = InstantiateNestElement(new DebugRoadData()
                            {From = n1.WorldPosition, To = n2.WorldPosition, IsUnderConstruction = false});
                        var roadNodes = road.GetNodes();
                        var r1 = roadNodes.ElementAt(0);
                        var r2 = roadNodes.ElementAt(1);

                        ConnectElements(n1, r1);
                        ConnectElements(r2, n2);
                    }
                }

                if (GUILayout.Button("ランダムな経路探索のテスト"))
                {
                    var ne = NestElements[Random.Range(0, NestElements.Count)];
                    var node1 = ne.GetNodes().ElementAt(Random.Range(0, ne.GetNodes().Count()));
                    ne = NestElements[Random.Range(0, NestElements.Count)];
                    var node2 = ne.GetNodes().ElementAt(Random.Range(0, ne.GetNodes().Count()));

                    latestRoute = FindRoute(node1, node2);
                }

                showGraph = GUILayout.Toggle(showGraph, "経路グラフを表示");
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGraph) return;
            foreach (var e in nestElements)
            {
                Gizmos.color = Color.green;
                foreach (var edge in e.GetLocalEdges())
                {
                    var a = edge.A.WorldPosition;
                    var b = edge.B.WorldPosition;
                    Gizmos.DrawLine(a, b);
                }
            }

            Gizmos.color = Color.red;
            foreach (var edge in elementEdges)
            {
                var a = edge.A.WorldPosition;
                var b = edge.B.WorldPosition;
                Gizmos.DrawLine(a, b);
            }

            if (latestRoute != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < latestRoute.Count() - 1; i++)
                {
                    var a = latestRoute.ElementAt(i);
                    var b = latestRoute.ElementAt(i + 1);
                    Gizmos.DrawLine(a.WorldPosition, b.WorldPosition);
                }
            }
        }

        #endregion
    }
}