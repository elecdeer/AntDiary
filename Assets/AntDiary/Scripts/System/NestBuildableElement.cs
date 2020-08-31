using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AntDiary
{
    public abstract class NestBuildableElement : NestElement
    {
        public abstract NestBuildableElementData DataAsBuildableElement { get; }
        
        
        
        /// <summary>
        /// 建築の完了に必要な労働コスト
        /// </summary>
        public abstract float RequiredResources { get; }

        public bool IsUnderConstruction
        {
            get => DataAsBuildableElement.IsUnderConstruction;
        }

        /// <summary>
        /// 建築のために労働コストを投入する。
        /// </summary>
        public void Commit(float buildingResource)
        {
            if (!IsUnderConstruction) return;

            DataAsBuildableElement.BuildingProgress += buildingResource;
            if (DataAsBuildableElement.BuildingProgress >= RequiredResources)
            {
                DataAsBuildableElement.IsUnderConstruction = false;
                DataAsBuildableElement.BuildingProgress = 0;
                NestSystem.Instance.OnChangeNestPath();
                OnBuildingCompleted();
            }
        }

        /// <summary>
        /// 建築が完了したときに呼ばれる。
        /// </summary>
        protected virtual void OnBuildingCompleted()
        {
        }

        /// <summary>
        /// このNestBuildableElementを建築する際に建築アリが行くべきノードを示す。
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<IPathNode> GetBuildingNode()
        {
            return GetNodes().Where(n => n.IsExposed).SelectMany(n => n.GetConnectedNodesForeign(true));
        }
    }

    public abstract class NestBuildableElement<T> : NestBuildableElement where T : NestBuildableElementData
    {
        
        /// <summary>
        /// 外部公開用のData
        /// </summary>
        public sealed override NestElementData Data => SelfData;

        /// <summary>
        /// DataをNestBuildableElementとして取得します
        /// </summary>
        public sealed override NestBuildableElementData DataAsBuildableElement => SelfData;
        
        /// <summary>
        /// クラス内から参照する用のプロパティ
        /// </summary>
        protected T SelfData { get; private set; }
        
        protected bool IsInitialized { get; private set; } = false;
        
        /// <summary>
        /// NestSystemがデータを注入する際に使用します。そのほかでは呼ばないでください。 
        /// </summary>
        /// <param name="antData"></param>
        public void Initialize(T antData)
        {
            if (IsInitialized) return;
            SelfData = antData;
            IsInitialized = true;
            gameObject.layer = LayerMask.NameToLayer("NestElement");
            OnInitialized();
        }

        /// <summary>
        /// 初期化が終了（NestBuildavleElementDataの注入が完了）したタイミングで呼ばれる。
        /// </summary>
        protected virtual void OnInitialized()
        {
        }
        
    }
}