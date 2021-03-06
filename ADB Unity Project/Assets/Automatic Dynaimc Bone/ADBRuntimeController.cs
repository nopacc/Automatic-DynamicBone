﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace ADBRuntime
{
    public enum ColliderCollisionType
    {
        /// <summary>
        /// Accurate but slow
        /// </summary>
        Accuate=1,
        /// <summary>
        /// Route but fast
        /// </summary>
        Fast=2,
        /// <summary>
        /// 没有碰撞(最快)
        /// </summary>
        Null=3
    }
    [DisallowMultipleComponent]
    public class ADBRuntimeController : MonoBehaviour
    {
        [SerializeField]
        public bool isGenerateColliderAutomaitc=true;
        [SerializeField]
        public bool isGenerateByFixedPoint = true;
        [SerializeField]
        public float delayTime=0.0001f;
        [SerializeField]
        public int iteration=4;
        [SerializeField]
        public float windScale=0.5f;
        public bool isDebug;
        public bool isResetPoint;
        [SerializeField]
        public ADBGlobalSetting settings;
        public ColliderCollisionType colliderCollisionType = ColliderCollisionType.Accuate;
        [SerializeField]
        public List<string> generateKeyWordWhiteList = new List<string> { "skirt" };// "hair", "tail", 
        [SerializeField]
        public List<string> generateKeyWordBlackList = new List<string> { "ik" };
        [SerializeField]
        public List<Transform> blackListOfGenerateTransform;

        public Transform generateTransform;
        public List<Transform> allPointTrans;
        [SerializeField]
        public List<ADBEditorCollider> editorColliderList;

        public float deltaTime { get; private set; }
        public ADBRuntimeWind windForcePower { get; private set; }

        private ADBRuntimeColliderControll colliderControll;
        private ADBConstraintReadAndPointControll[] jointAndPointControlls;
        private DataPackage dataPackage;
        private bool isInitialize = false;
        private float initializeScale;
        private float scale;
        private Vector3 windForce;
        private void Start()//OYM：滚回来自己来趟这趟屎山
        {
            if (!isInitialize)
            {
                initializePoint();
                initializeCollider(false, isGenerateByFixedPoint);
            }

            if (jointAndPointControlls == null) return;

            dataPackage = new DataPackage();
            if (Application.isPlaying)
            {
                isInitialize = true;
                for (int i = 0; i < jointAndPointControlls.Length; i++)
                {
                    jointAndPointControlls[i].GetData(ref dataPackage);//OYM：在这里对各种joint和point进行分类与编号
                }
                colliderControll.GetData(ref dataPackage);
                dataPackage.SetNativeArray();
                initializeScale = transform.lossyScale.x;
                isInitialize = true;
            }
            delayTime = delayTime < 0.001f ? 0.001f : delayTime;
        }

        private void OnDrawGizmos()
        {       
            if(!isDebug) return;

            if (jointAndPointControlls != null)
            {
                foreach (var controll in jointAndPointControlls)
                {
                    controll.OnDrawGizmos();
                }
            }


                if (colliderControll != null&&Application.isPlaying)
                {
                    Gizmos.color = Color.red;
                    colliderControll.OnDrawGizmos();
                }
        }

        private void Update()
        {
            if (jointAndPointControlls == null) return;

            if (delayTime - Time.deltaTime > 0)
            {
                delayTime -= Time.deltaTime;
                return;
            }
            else if (( delayTime > 0 && delayTime - Time.deltaTime < 0)|| isResetPoint)
            {
                delayTime -= Time.deltaTime;
                RestorePoint();
                return;
            }
            if(isResetPoint)
            {
                RestorePoint();
                isResetPoint = false;
                return;
            }
             deltaTime = Mathf.Min(Time.deltaTime, 0.016f);
            scale = transform.lossyScale.x / initializeScale;

            windForce = ADBWindZone.getWindForce(transform.position, deltaTime * windScale) * windScale;
                UpdateDataPakage();
 
        }
        private void OnDisable()
        {
            RestorePoint();
        }
        private void OnDestroy()
        {
            RestorePoint();
            dataPackage.Dispose();
        }
        public void RestorePoint()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("Use it On Runtime!");
                return;
            }
            dataPackage.restorePoint();
        }
        private void UpdateDataPakage()
        {
            dataPackage.SetRuntimeData(deltaTime, scale / initializeScale, iteration, windForce, colliderCollisionType);
        }

        internal void GenerateNewOne()
        {
            delayTime = 0.5f;
            isInitialize = false;
            Start();
        }

        public void initializeList()
        {//OYM：一个简单的防报错和把关键词tolower的方法

            if (!(generateKeyWordWhiteList?.Count != 0))
            {
                generateKeyWordWhiteList = settings.defaultKeyWord;
                if (generateKeyWordWhiteList == null)
                {
                    Debug.Log("The white key is null!Check the RuntimeController or Global Setting!");
                    return;
                }
            }
            else
            {
                for (int i = 0; i < generateKeyWordWhiteList.Count; i++)
                {
                    generateKeyWordWhiteList[i] = generateKeyWordWhiteList[i].ToLower();
                }
            }
            for (int i = 0; i < generateKeyWordBlackList.Count; i++)
            {
                generateKeyWordBlackList[i] = generateKeyWordBlackList[i].ToLower();
            }
            if (!generateTransform)
            {
                generateTransform = transform;
            }
            if (settings == null)
            {
                settings = Resources.Load("Setting/ADBGlobalSettingFile") as ADBGlobalSetting;
            }
        }

        public void initializePoint()
        {
            initializeList();
            jointAndPointControlls = ADBConstraintReadAndPointControll.GetJointAndPointControllList(generateTransform, generateKeyWordWhiteList, generateKeyWordBlackList, blackListOfGenerateTransform,settings);//OYM：在这里搜索所有的节点和杆件的controll
            allPointTrans = new List<Transform>();
            if (jointAndPointControlls != null)
            {

                for (int i = 0; i < jointAndPointControlls.Length; i++)
                {
                    jointAndPointControlls[i].Initialize();//OYM：在这里对各种joint和point进行分类与编号
                    for (int j0 = 0; j0 < jointAndPointControlls[i].allNodeList.Count; j0++)
                    {
                        allPointTrans.Add(jointAndPointControlls[i].allNodeList[j0].trans);
                    }
                }
            }
            else
            {
                Debug.Log( "no point found , check the white key word");
            }
        }

        public void initializeCollider(bool generateScript, bool isFixedPoint)
        {
            List<ADBRuntimePoint> list = null;
            if (jointAndPointControlls == null || jointAndPointControlls.Length == 0)
            {
                Debug.Log("use <generate Point> to get higher accuate collider");
            }
            else
            {
                list = new List<ADBRuntimePoint>();
                for (int i = 0; i < jointAndPointControlls.Length; i++)
                {
                    if (isFixedPoint)
                    {
                        list.AddRange(jointAndPointControlls[i].fixedNodeList);
                    }
                    else
                    {
                        list.AddRange(jointAndPointControlls[i].allNodeList);
                    }
                }
            }

            colliderControll = new ADBRuntimeColliderControll(gameObject, list, isGenerateColliderAutomaitc);//OYM：在这里获取collider
            if (generateScript)
            {
                editorColliderList = new List<ADBEditorCollider>();
                var OtheerColliderList = gameObject.GetComponentsInChildren<ADBEditorCollider>();
                for (int i = 0; i < colliderControll.runtimeColliderList.Count; i++)
                {
                    if (colliderControll.runtimeColliderList[i].appendTransform == null) continue;

                    ADBEditorCollider.RuntimeCollider2Editor(colliderControll.runtimeColliderList[i],ref editorColliderList);
                }
                editorColliderList.AddRange(OtheerColliderList);
                for (int i = 0; i < editorColliderList.Count; i++)
                {
                    editorColliderList[i].isDraw = true;
                }
                isGenerateColliderAutomaitc = false;
            }
        }

    }
}