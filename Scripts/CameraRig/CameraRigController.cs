using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum CameraMode
{
    Locked,
    Free
}

public class CameraRigController : MonoBehaviour
{
    public CameraRigController instance;
    public CinemachineTargetGroup BattleGroup;
    public CinemachineVirtualCamera BattleCam;
    public CinemachineVirtualCamera ActiveCam;
    public List<CinemachineVirtualCamera> UnitCams;


    [Header("Camera Rig Settings")]
    public float panSpeed = 75f;
    public float zoomSpeed = 100f;

    public static float maxPanUpDown = 5f;
    public static float maxPanLeftRight = 60f;

    public static Vector3 defaultAim = new Vector3(0, 50, 0);

    public void ResetBattleGroup() 
    {
        while(BattleGroup.m_Targets.Length > 0) 
        {
            BattleGroup.RemoveMember(BattleGroup.m_Targets[0].target);
        }
    }

    public void SetupBattleCam(List<UnitController> units) 
    {
        ResetBattleGroup();
        foreach (UnitController unit in units) 
        {
            BattleGroup.AddMember(unit.transform, 1f, 0);
        }
        ActiveCam.Priority = 1;
        BattleCam.Priority = 10;

    }

    public void LockOnUnit(GameObject target) 
    {
        ActiveCam = target.GetComponentInChildren<CinemachineVirtualCamera>();
        foreach (CinemachineVirtualCamera cam in UnitCams) 
        {
            if (cam == ActiveCam)
            {
                cam.Priority = 10;
            }
            else 
            {
                cam.Priority = 1;
            }
        }
    } 

    public CameraRigController SetupCameraRig(GameManager manager)
    {
        instance = this;
        UnitCams = new List<CinemachineVirtualCamera>();

        foreach (UnitController hero in manager.Heroes.Values) 
        {
            UnitCams.AddRange(hero.gameObject.GetComponentsInChildren<CinemachineVirtualCamera>());
        }

        foreach (Dictionary<UnitController, EnemyCamp> camp in manager.Camps)
        {
            foreach (UnitController unit in camp.Keys) 
            {
                UnitCams.AddRange(unit.gameObject.GetComponentsInChildren<CinemachineVirtualCamera>());
            }   
        }
        return instance;
    }

    public void LookAround() 
    {
        CinemachineComposer composer = ActiveCam.GetCinemachineComponent<CinemachineComposer>();
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
        {
            if (Input.GetKey(KeyCode.W))
            {
                float targetHeight = Mathf.Clamp(composer.m_TrackedObjectOffset.y + panSpeed * Time.deltaTime, defaultAim.y - maxPanUpDown, defaultAim.y + maxPanUpDown);
                composer.m_TrackedObjectOffset = new Vector3(composer.m_TrackedObjectOffset.x, targetHeight, 0);
            }

            if (Input.GetKey(KeyCode.S))
            {
                float targetHeight = Mathf.Clamp(composer.m_TrackedObjectOffset.y - panSpeed * Time.deltaTime, defaultAim.y - maxPanUpDown, defaultAim.y + maxPanUpDown);
                composer.m_TrackedObjectOffset = new Vector3(composer.m_TrackedObjectOffset.x, targetHeight, 0);
            }

            if (Input.GetKey(KeyCode.A))
            {
                float targetWidth = Mathf.Clamp(composer.m_TrackedObjectOffset.x - panSpeed * Time.deltaTime, defaultAim.x - maxPanLeftRight, defaultAim.x + maxPanLeftRight);
                composer.m_TrackedObjectOffset = new Vector3(targetWidth, composer.m_TrackedObjectOffset.y, 0);
            }

            if (Input.GetKey(KeyCode.D))
            {
                float targetWidth = Mathf.Clamp(composer.m_TrackedObjectOffset.x + panSpeed * Time.deltaTime, defaultAim.x - maxPanLeftRight, defaultAim.x + maxPanLeftRight);
                composer.m_TrackedObjectOffset = new Vector3(targetWidth, composer.m_TrackedObjectOffset.y, 0);
            }
        }
        else
        {
            composer.m_TrackedObjectOffset = defaultAim;
        }
    }

    public void ZoomInOut() 
    {
        if (Input.GetAxisRaw("Mouse ScrollWheel")!=0) 
        {
            CinemachineFramingTransposer transposer = ActiveCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            transposer.m_CameraDistance = Mathf.Clamp(transposer.m_CameraDistance+zoomSpeed*Input.GetAxisRaw("Mouse ScrollWheel"), 100f, 600f);
        }
    }

    public void Update()
    {
        LookAround();
        ZoomInOut();
    }
}