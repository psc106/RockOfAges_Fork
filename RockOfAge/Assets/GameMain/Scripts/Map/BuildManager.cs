using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

public class BuildManager : MonoBehaviourPun/*, IPunObservable*/
{
    public static BuildManager instance;
    public ObstacleBase buildTarget = null;


    //!!!!!!!!!!!!!!!!!!!!!!!!!테스트 코드
    //해당 정보는 item manager에 저장될 예정??
    //상의 해 봐야함
    public GameObject gridTest;
    public Material red;
    public Material white;

    //건물이 바라보는 방향
    private BuildRotateDirection whereLookAt;
    //드래그 중인 방향
    private BuildDragDirection whereDragCursor;
    //커서의 현재 그리드위치
    private Vector3Int currCursorGridIndex = Vector3Int.zero;
    //좌클릭한 곳의 좌표
    private Vector3Int clickGridIndex = Vector3Int.zero;
    //좌클릭한 곳의 좌표
    private Vector3Int endGridIndex = Vector3Int.zero;

    private List<Vector3Int> dragBuildPosition = default;

    //빌드 뷰어
    //렌더러만 가진 Viewer 오브젝트
    private BuildViewer buildViewer;
    private DragViewer dragViewer;

    //해당 지형의 건설 가능 상태를 저장 
    private BitArray buildState;
    private Vector3 gridOffset;

    public bool isLeftClick = false;

    //맵 사이즈 
    public static readonly int MAP_SIZE_X = 256;
    public static readonly int MAP_SIZE_Z = 256;
    public static readonly int MAP_SIZE_Y = 50;
    public static readonly Vector3 FIXED_GRID_OFFSET = (Vector3.one - Vector3.up) * .5f;
    public static readonly Vector3Int[] DIFF_VECTOR = new Vector3Int[] { Vector3Int.right, Vector3Int.forward, Vector3Int.left, Vector3Int.back };

    const int ONCE_ROTATE_EULER_ANGLE = 90;
    public static readonly Vector3Int OUT_VECTOR = new Vector3Int(-90000, 0, 0);


/*
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {

        if (stream.IsReading) 
        {
            stream.SendNext(buildState);
        }
        else  
        {
            buildState = (BitArray)stream.ReceiveNext();

        }
    }*/



    private void Awake()
    {
        instance = this;

        buildState = new BitArray(MAP_SIZE_X * MAP_SIZE_Z);
        buildViewer = GetComponentInChildren<BuildViewer>();
        buildViewer.HideViewer();
        dragViewer = GetComponentInChildren<DragViewer>();


        InitTerrainData();
    }
    void BuildStart()
    {
        for (int i = 0; i < dragBuildPosition.Count; i++)
        {
            //건설 시작
            Vector3 position;
            Quaternion rotation;

            //Viewer의 위치를 기반으로 생성
            GetViewerPosition(dragBuildPosition[i], Quaternion.Euler(0, (int)whereLookAt * ONCE_ROTATE_EULER_ANGLE - 90, 0), out position, out rotation);

            //드래그시 모습이 다른 건물일 경우(성벽)
            //drag 방향을 기반으로 회전
            if (buildTarget.dragObstacle && i != 0 && i != dragBuildPosition.Count - 1)
            {
                rotation = Quaternion.Euler(0, (int)whereDragCursor * ONCE_ROTATE_EULER_ANGLE, 0);
            }

            //ObstacleBase의 Build 메서드를 호출하여 건설
            ObstacleBase build = buildTarget.Build(position, rotation, i, dragBuildPosition.Count);
            build.name = buildTarget.name + "_" + currCursorGridIndex.z + "_" + currCursorGridIndex.x;

            //그리드의 건설 정보 변경
            SetBitArrays(dragBuildPosition[i], buildTarget.status.Size);
        }
    }

    //현재 게임 싸이클 상태에 따라서 진입한다. 
    //커서의 위치에 따른 현재 grid 값을 재조정하고
    //현재 grid 위치에 terrain이 있을 경우,
    //해당 지형이 건설 가능한지를 판단한다.
    void Update()
    {
        if(!IsDefance() && buildTarget != null)
        {
            return;
        }

        //현재 마우스의 좌표를 변경
        ChangeCurrGrid();
        ChangeViewerPosition();


        //우선순위 우클릭->좌클릭
        //우클릭시 타겟 데이터, 드래그 데이터 리셋
        if (Input.GetMouseButtonDown(1))
        {
            ResetTarget();
            ResetDragData();
            buildViewer.HideViewer();
            return;
        }
        //좌클릭시 좌표 갱신
        else if(Input.GetMouseButtonDown(0))
        {
            if (IsTerrain())
            {
                SaveStartGrid();
            }
        }

        //좌클릭동안 좌표 저장
        else if (Input.GetMouseButton(0))
        {
            SaveDragGrid();
        }        

        //좌클릭 떼는 순간 건설 시작
        else if (Input.GetMouseButtonUp(0))
        {
            //조건 검사
            if (CanBuild())
            {
                BuildStart();
            }
            ResetDragData();
        }

        //방해물 회전
        //방해물의 회전 정보는 manager에 저장된 회전 정보를 기반으로 설정된다.
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ChangeBuildRotation(-1);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            ChangeBuildRotation(1);
        }

        //Viewer의 활성화를 결장하는 단계
        if ((!IsDefance() || !IsTerrain()) && !isLeftClick)
        {
            buildViewer.HideViewer();
            return;
        }

        buildViewer.ShowViewer();
        buildViewer.UpdateColor(CanBuild());

    }

    private void ResetDragData()
    {
        dragViewer.EndDrag();

        isLeftClick = false;
        clickGridIndex = OUT_VECTOR;
        endGridIndex = OUT_VECTOR;

        dragBuildPosition = null;

    }

    //씬 실행시 map 건설 가능 상태를 init함
    //terrain 비교시 tag로 team, 기본 건설 불가 타일등을 비교하는 부분 추가해야할거같음
    public void InitTerrainData()
    {
        int checkLayer = Global_PSC.FindLayerToName("Terrains") + Global_PSC.FindLayerToName("Walls") 
            + Global_PSC.FindLayerToName("Obstacles") + Global_PSC.FindLayerToName("Environment") + Global_PSC.FindLayerToName("ETC");
        //모든 좌표를 검사한다.
        buildState.SetAll(false);
        for (int z = -MAP_SIZE_Z / 2; z < MAP_SIZE_Z / 2; z++)
        {
            for (int x = -MAP_SIZE_X / 2; x < MAP_SIZE_X / 2; x++)
            {
                RaycastHit raycastHit;
                if (Physics.Raycast(new Vector3(x, MAP_SIZE_Y, z) + (Vector3.one - Vector3.up) * .5f, Vector3.down, out raycastHit, float.MaxValue, checkLayer))
                {
                    //건설 불가 타일 검사(안부셔지는 장애물)
                    if (raycastHit.collider.gameObject.layer == LayerMask.NameToLayer("Walls"))
                    {
                        continue;
                    }

                    //건설 불가 타일 검사(부셔지는 장애물)
                    if (raycastHit.collider.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
                    {
                        continue;
                    }

                    //건설 불가 타일 검사(파괴 후 환경)
                    if (raycastHit.collider.gameObject.layer == LayerMask.NameToLayer("Environment"))
                    {
                        continue;
                    }

                    //건설 불가 타일 검사(파괴 전 환경)
                    if (raycastHit.collider.gameObject.layer == LayerMask.NameToLayer("ETC"))
                    {   
                        continue;
                    }

                    //건설 불가 타일 검사(특정 타일)
                    if (raycastHit.collider.CompareTag("Block"))
                    {
                        continue;
                    }


                    //건설 불가 타일 검사 통과시 
                    buildState.Set((z + MAP_SIZE_Z / 2) * MAP_SIZE_Z + (x + MAP_SIZE_X / 2), true);

                    ///////////////////////////////////////////////////////////테스트 코드
                    GameObject gameObject = Instantiate(gridTest, raycastHit.point + Vector3.up * 0.02f, Quaternion.FromToRotation(-Vector3.forward, raycastHit.normal));
                    gameObject.name = gridTest.name + "_" + z + "_" + x;
                    gameObject.transform.localScale = Vector3.one * 0.8f;
                    /////////////////////////////////////////////////////////////////////
                }
            }
        }
    }

    //size만큼의 건설 가능 데이터를 변경함
    public void SetBitArrays(Vector3 grid, Vector2Int buildSize)
    {

        //짝수 크기와 홀수 크기마다 맵핑되는 그리드 영역이 달라지기 때문에 적당한 계산 값을 도출해냈음
        //크기 3 일 경우
        //1.5 ~ -1.5 -> 1 ~ -1

        //크기가 2 일 경우
        //1 ~ -1 -> 1 ~ 0
        for (int y = (int)(buildSize.y * .5f); y > -(buildSize.y * .5f); y--)
        {
            for (int x = (int)(buildSize.x * .5f); x > -(buildSize.x * .5f); x--)
            {
                //입력된 그리드를 기반으로 그리드 위치 변경
                Vector3 _grid = grid - Vector3Int.right * x - Vector3Int.forward * y;

                int size = (int)((_grid.z + MAP_SIZE_Z * .5f) * MAP_SIZE_Z + (_grid.x + MAP_SIZE_X * .5f));
                if (size < 0 || MAP_SIZE_X * MAP_SIZE_Z <= size)
                {
                    return;
                }
                //건설 가능 데이터 변경
                photonView.RPC("SetBitArrayInRpc", RpcTarget.All, size);

                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Test 코드
                //gameobject에 접근

                GameObject floor = GameObject.Find("GridTestCube_" + _grid.z + "_" + _grid.x);
                if(floor!=null)
                    floor.GetComponent<MeshRenderer>().material = red;
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            }
        }

    }

    [PunRPC]
    public void SetBitArrayInRpc(int size)
    {
        buildState.Set(size, false);
    }


    //각 grid를 순회하며 size만큼의 건설 가능 데이터를 변경함
    void SetBitArrays(List<Vector3> grids, Vector2Int buildSize)
    {
        for (int i = 0; i < grids.Count; i++)
        {
            SetBitArrays(grids[i], buildSize);
        }

    }

    //obstacle, 즉 장애물이 변경될시 불러온다.
    public void ChangeBuildTarget(ObstacleBase target)
    {
        buildTarget = target;

        //맵핑되는 그리드의 좌표를 변경하기 위해 offset 설정
        //짝수
        //0, 0
        //홀수
        //0.5, 0.5
        gridOffset = new Vector3((buildTarget.status.Size.x) % 2 * .5f, 0, (buildTarget.status.Size.y) % 2 * .5f);
        buildViewer.UpdateTarget(target);
        //gridOffset = new Vector3((int)(buildTarget.size.x + 1) % 2 * .5f, 0, (int)(buildTarget.size.y + 1) % 2 * .5f);

    }


    //마우스가 이동할때마다 불러온다.
    bool ChangeCurrGrid()
    {
        //마우스 커서는 기본적으로 (좌하단 0,0)을 기준으로 계산된다.
        //그렇기 때문에 마우스 커서를 가져온뒤 이를 월드 좌표로 변환한다.

        Vector3 mouseWorldPos = Global_PSC.GetWorldMousePositionFromMainCamera();

        Vector3Int _currCursorGridIndex;

        //짝수인지 홀수인지에 따라서 맵핑되는 그리드 좌표 달라짐.
        if (gridOffset.x == .5f)
        {
            //홀수
            _currCursorGridIndex = new Vector3Int(Mathf.FloorToInt(mouseWorldPos.x), 0, Mathf.FloorToInt(mouseWorldPos.z));
        }
        else
        {
            //짝수
            _currCursorGridIndex = new Vector3Int(Mathf.RoundToInt(mouseWorldPos.x), 0, Mathf.RoundToInt(mouseWorldPos.z));
        }

        //커서가 이전과 다를 경우 데이터를 갱신
        if (currCursorGridIndex != _currCursorGridIndex)
        {
            currCursorGridIndex = _currCursorGridIndex;
            return true;
        }
        return false;
    }

    //수정되야 한다.
    //drag 가능한 오브젝트 -> 클릭한 위치 기준으로 오브젝트가 그려져야한다.

    //그외
    //1. 클릭한 상태일 경우 - 클릭한 위치 기준으로 viwer가 그려져야 한다.
    //2. 클릭 안한 상태일 경우 - 바로바로 갱신 되어야 한다.

    //grid 정보가 바뀔때마다 불러온다.
    //viewer 위치를 갱신한다.
    Vector3Int ChangeViewerPosition()
    {
        if (!isLeftClick)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(currCursorGridIndex + gridOffset + Vector3.up * MAP_SIZE_Y, Vector3.down, out raycastHit, float.MaxValue, Global_PSC.FindLayerToName("Terrains")))
            {
                Vector3 newPosition = raycastHit.point + buildViewer.transform.up * (buildViewer.gameObject.GetHeight(.1f) * .5f);
                buildViewer.transform.position = newPosition;
                return new Vector3Int((int)raycastHit.point.x, (int)raycastHit.point.y, (int)raycastHit.point.z);
            }
        }
        return OUT_VECTOR;
    }

    Vector3Int SaveDragData()
    {
        //if (buildTarget.dragObstacle)
        {
            Vector3 gridDiff = clickGridIndex - endGridIndex;
            //Debug.Log(gridDiff);

            float absX = Mathf.Abs(gridDiff.x);
            float absZ = Mathf.Abs(gridDiff.z);

            int length;
            int size;
            Vector2 sizeVector = buildTarget.status.Size;

            //월드좌표 수평
            if (absX > absZ)
            {
                length = (int)(absX / sizeVector.x);
                size = (int)sizeVector.x;
                if (gridDiff.x < 0)
                {
                    whereDragCursor = BuildDragDirection.UP;
                }
                else
                {
                    whereDragCursor = BuildDragDirection.DOWN;
                }
            }
            //월드좌표 수직
            else
            {
                length = (int)(absZ / sizeVector.y);
                size = (int)sizeVector.y;
                if (gridDiff.z < 0)
                {
                    whereDragCursor = BuildDragDirection.RIGHT;
                }
                else
                {
                    whereDragCursor = BuildDragDirection.LEFT;
                }
            }

            dragBuildPosition = new List<Vector3Int>();
            for (int i = 0; i <= length; i++)
            {
                dragBuildPosition.Add(clickGridIndex + DIFF_VECTOR[(int)whereDragCursor] * size * i);
            }
            return dragBuildPosition[length];

            //이후 빌드뷰어에서 여러개 생성하는 메서드 추가.
        }
    }


    //수정할 필요가 있을수도?
    //해봐야 알듯?

    //특정키 누를시 방향 회전 시킨다.
    //이 정보는 유지된다.
    void ChangeBuildRotation(int diff)
    {
        if (whereLookAt == BuildRotateDirection.LEFT && diff == 1)
        {
            whereLookAt = BuildRotateDirection.UP;
        }
        else if (whereLookAt == BuildRotateDirection.UP && diff == -1)
        {
            whereLookAt = BuildRotateDirection.LEFT;
        }
        else
        {
            whereLookAt = whereLookAt + diff;
        }
        Vector3 localEuler = buildViewer.transform.localEulerAngles;
        buildViewer.transform.localEulerAngles =new Vector3(localEuler.x, ONCE_ROTATE_EULER_ANGLE * (int)whereLookAt, localEuler.z);
    }

    void SetClickPosition(Vector3Int position)
    {
        clickGridIndex = position;
    }

    void SaveStartGrid()
    {
        isLeftClick = true;
        //시작 좌표 저장
        clickGridIndex = currCursorGridIndex;
        endGridIndex = currCursorGridIndex;
        dragViewer.StartDrag(clickGridIndex);

    }
    void SaveDragGrid()
    {
        endGridIndex = currCursorGridIndex;
        Vector3 end = SaveDragData();

        if (GetBuildEnable(end))
        {
            dragViewer.ContinueDrag(end);
        }
        else
        {
            dragViewer.ContinueDrag(clickGridIndex);
        }
    }

    void ResetTarget()
    {
        buildTarget = null;

    }

    bool IsUIClick()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }

    //모드 참조
    bool IsDefance()
    {
        if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName("PscTestScene"))
        {

            return true;
        }

        if (CycleManager.cycleManager.userState == (int)UserState.DEFENCE)
        {
            return true;
        }
        return false;
    }

    //현재 그리드 위치에 건설 가능한 지형이 존재하는지 체크
    bool IsTerrain()
    {
        RaycastHit raycastHit;
        if (Physics.Raycast(currCursorGridIndex + gridOffset + Vector3.up * MAP_SIZE_Y, Vector3.down, out raycastHit, float.MaxValue, Global_PSC.FindLayerToName("Terrains")))
        {
            return true;
        }

        return false;
    }


    //아이템의 limit상태와 해당 terrain의 건설 가능 상태를 &&한다.
    bool CanBuild()
    {

        if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName("PscTestScene"))
        {

           // return true;
        }
        //Debug.Log("?");

        if (buildTarget == null)
        {
            return false;
        }

        if (isLeftClick)
        {

            return GetBuildEnable(dragBuildPosition, buildTarget.status.Size) && GetItemLimitState(dragBuildPosition.Count);
        }
        else
        {
            return GetBuildEnable(currCursorGridIndex, buildTarget.status.Size) && GetItemLimitState(1);
        }

    }

    bool GetViewerPosition(Vector3Int position, Quaternion rotation, out Vector3 outPosition, out Quaternion outRotation)
    {
        RaycastHit raycastHit;
        if (Physics.Raycast(position + gridOffset + Vector3.up * MAP_SIZE_Y, Vector3.down, out raycastHit, float.MaxValue, Global_PSC.FindLayerToName("Terrains")))
        {
            outPosition = raycastHit.point + Vector3.up * buildTarget.buildPositionY;
            outRotation = Quaternion.FromToRotation(Vector3.up, raycastHit.normal) * rotation;
            return true;
        }
        outPosition = OUT_VECTOR;
        outRotation = Quaternion.identity;
        return false;
    }

    //현재 건설된 아이템의 최대 건설 개수와 현재 건설 개수를 비교한다.
    //true : 현재 건설 개수가 최대 건설보다 낮다 && 소지 골드가 설치비용보다 크거나 작다
    bool GetItemLimitState(int size)
    {
        if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName("PscTestScene"))
        {

            return true;
        }
        
        if(buildTarget == null)
        {
            return false;
        }
        // gold & limit
        float gold = default;
        int buildLimit = default;
        ResourceManager.Instance.GetUnitGoldAndBuildLimitFromID(buildTarget.status.Id, out gold, out buildLimit);
        GameObject unitButton = ResourceManager.Instance.FindUnitGameObjById(buildTarget.status.Id);

        int buildCount = 0;
        if (unitButton != null) 
        {
            buildCount = unitButton.GetComponent<CreateButton>().buildCount;
        
        }
        return (buildCount+size <= buildLimit &&
            gold * size <= NetworkManager.Instance.myDataContainer.GetComponent<PlayerDataContainer>().playerGold);
    }

    //현재 grid위치의 주변 위치의 terrain의 상태를 전부 비교
    bool GetBuildEnable(List<Vector3Int> grids, Vector2Int buildSize)
    {
        bool result = true;

        for (int i = 0; i < grids.Count;i++ )
        {
            result = result && GetBuildEnable(grids[i], buildSize);
            if (!result)
            {
                return result;
            }
        }
        return result;
    }

    //현재 grid위치의 주변 위치의 terrain의 상태를 전부 비교
    bool GetBuildEnable(Vector3Int grid, Vector2Int buildSize)
    {
        bool result = true;

        for (int y = (int)(buildSize.y * .5f); y > -buildSize.y * .5f; y--)
        {
            for (int x = (int)(buildSize.x * .5f); x > -buildSize.x * .5f; x--)
            {
                result = result && GetBuildEnable(grid - Vector3Int.right * x - Vector3Int.forward * y);
                
            }
        }
        return result;
    }

    //그리드 좌표에 따라 건설 가능 데이터를 가져오는 메서드
    bool GetBuildEnable(Vector3 grid)
    {
        int size = (int)((grid.z + MAP_SIZE_Z * .5f) * MAP_SIZE_Z + (grid.x + MAP_SIZE_X * .5f));
        if(size < 0 || MAP_SIZE_X *MAP_SIZE_Z <= size)
        {
            return false;
        }
         
        return buildState.Get(size);
    }

}





public enum BuildRotateDirection
{
    UP = 0,
    RIGHT = 1,
    DOWN = 2,
    LEFT = 3
}

public enum BuildDragDirection
{
    UP = 0,
    RIGHT = 1,
    DOWN = 2,
    LEFT = 3
}

