using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AntDiary{
	/// <summary>
	/// 建築中のElementがなく、待機してるときのStrategy
	/// </summary>
	public class RoundStrategy: BuilderStrategy{
		public RoundStrategy(BuilderAnt ant) : base(ant){
			UpdateInterval = 1.0f;
		}

		public override void StartStrategy(){
			
		}

		public override void PeriodicUpdate(){
			//到達可能なのはすでに建築済みのNodeだけだが、欲しいのはそのNodeに重なった建築中のNode
			
			var buildingElements = NestSystem.Instance.BuildingElements;
			var currentNode = HostAnt.SupposeCurrentPosNode();
			
			Debug.Log($"<RoundStrategy> PeriodicUpdate building?: {buildingElements.Any()} ant:{currentNode?.WorldPosition} on {currentNode?.Host.transform.position}");
			
			if(!buildingElements.Any()) return;
			if(currentNode == null) return;
			

			//建築中のElementの各ノードとそのホスト(建築中)をペアにして、それを現在位置との直線距離の短い順でソート
			//TODO 厳密には移動コスト順にすべき
			var buildingNodes = buildingElements
				.SelectMany(elem => elem.GetBuildingNode().Select(node => {
					var pathNode = node as NestPathNode;
					return (elem, pathNode);
				}).ToList())
				.OrderBy(tuple => Vector3.Distance(tuple.pathNode.WorldPosition, currentNode.WorldPosition));

			
			// Debug.Log("buildingNodes");
			// foreach(var tuple in buildingNodes){
			// 	Debug.Log($"  [{tuple.Item2.WorldPosition}] name:{tuple.Item2.Name} host:{tuple.Item1.transform.position}");
			// }
			
			var distTuple = buildingNodes.FirstOrDefault(tuple =>
				NestSystem.Instance.FindRoute(tuple.pathNode, currentNode).Count() >= 2);
			
			
			HostAnt.ChangeStrategy(new MoveStrategy(HostAnt, distTuple.elem, distTuple.pathNode));


		}

		public override void FinishStrategy(){
			
		}
	}
}