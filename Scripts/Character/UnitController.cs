using Cinemachine;
using System;
using System.Drawing;
using UnityEngine;
using UnityEngine.AI;
using System.Threading.Tasks;
using UnityEngine.Windows.WebCam;
using System.Collections.Generic;

public enum UnitState
{
    Idle,
    Moving,
    Patrolling,
    AwaitingCombatTurn,
    Attacking,
    Evading,
    GettingHit,
    Dying,
    Dead
}

public class UnitController : MonoBehaviour
{
    public EnemyCamp campIn;
    public GameManager gameManager;
    public HexTile targetTile;
    public HexTile currentTile;
    public bool returnFlag;
    public HexTile initialTile;

    public bool baseBoostRoll;
    public bool baseBoostReveal;
    public bool baseBoostShown;
    public bool combatStartedMoving;

    public UnitController targetUnit;

    public bool prioritizeLockedCam = false;
    public bool revealInitiative = false;
    public bool revealedInitiave = false;
    public bool turnPlayed = false;

    [Header("Sprite Settings")]
    public GameObject landSprite;
    public GameObject sailSprite;

    public NavMeshAgent OnLandNavAgent;
    float Speed=120;
    float AngularSpeed=360;
    float Acceleration=120;
    float ObstacleAvoidanceRadius=6;
    float StoppingDistance=0;
    public Vector3 moveDirection = Vector3.zero;

    [Header("Sea Travel Settings")]
    SailNavAgent SailingNavAgent;
    private Path currentPath;
    

    [Header("Unit State Settings")]
    public UnitBase Unit;
    public Animator Animator;
    public UnitState State;
    public RollDice diceRoller;
    public CinemachineVirtualCamera LockedCam;
    public CinemachineVirtualCamera DiceCam;

    public void InitHero(GameManager gameManager)
    {
        this.gameManager = gameManager;
        currentTile = gameManager.grid.randomWaterTile();
        currentTile.node.occupied = true;
        transform.position = currentTile.node.worldPoint;
        landSprite.SetActive(false);
        sailSprite.SetActive(true);
        SailingNavAgent = new SailNavAgent();
        if (Unit.heroClass == HeroClass.MysticCorsair)
        {
            Animator.SetInteger("AttackType", 1);
        }
        else if (Unit.heroClass == HeroClass.Sharpshooter)
        {
            Animator.SetInteger("AttackType", 2);
        }
    }

    public void InitEnemy(GameManager gameManager, EnemyCamp campSpawnedIn) 
    {
        campIn = campSpawnedIn;
        this.gameManager = gameManager;
        // MUST PLACE THE ENEMIES SOMEHOW
        currentTile = campSpawnedIn.ClusterOn.centerTile.RandomLandNeighbour();
        initialTile = currentTile;
        currentTile.node.occupied = true;
        transform.position = currentTile.node.worldPoint;

        Collider[] colliders = new Collider[10]; // Array to store colliders
        int numColliders = Physics.OverlapBoxNonAlloc(gameObject.transform.position, gameObject.transform.localScale / 2f, colliders, Quaternion.identity);

        for (int i = 0; i < numColliders; i++)
        {
            if (colliders[i].gameObject != gameObject && colliders[i].gameObject.layer == LayerMask.NameToLayer("Environment"))
            {
                colliders[i].gameObject.SetActive(false);
                Destroy(colliders[i].gameObject);
                break;
            }
        }
    }

    public void SetAnimatorState(UnitState targetState, bool flag) 
    {
        Animator.SetBool(targetState.ToString(), flag);
    }

    void Update()
    {
        if (gameManager.GetGameState() == GameState.Explore)
        {
            switch (State)
            {
                case UnitState.Idle:
                    if (Unit.heroClass != HeroClass.Null)
                    {
                        if (gameManager.Heroes[gameManager.activeHeroIndex] == this)
                        {

                            if (targetTile != null)
                            {
                                if (currentTile.terrain == TileType.Water)
                                {
                                    currentPath = SailingNavAgent.AStar(currentTile.node, targetTile.node);
                                    if (currentPath != null)
                                    {
                                        SetAnimatorState(UnitState.Moving, true);
                                        State = UnitState.Moving;
                                    }
                                }
                                else if (currentTile.dockTile && targetTile.terrain == TileType.Water)
                                {
                                    DestroyLandNav();
                                    currentPath = SailingNavAgent.AStar(currentTile.node, targetTile.node);
                                    if (currentPath != null)
                                    {
                                        SetAnimatorState(UnitState.Moving, true);
                                        State = UnitState.Moving;
                                    }
                                }
                                else if (targetTile.island.TileBelongsToIsland(currentTile))
                                {
                                    SetupLandNav();
                                    OnLandNavAgent.SetDestination(new Vector3(targetTile.node.x, targetTile.centerHeight, targetTile.node.z));
                                    SetAnimatorState(UnitState.Moving, true);
                                    State = UnitState.Moving;
                                }
                            }
                        }

                    }
                    // ENEMY UNITS CONTROL
                    else
                    {
                        // if the active hero is on the same island, start patrolling (mob enemies)
                        if (gameManager.Heroes[gameManager.activeHeroIndex].currentTile.island == this.currentTile.island)
                        {
                            StartPatrol();
                            State = UnitState.Patrolling;
                        }
                    }
                    break;
                case UnitState.Moving:
                    if (sailSprite.active || (currentTile.dockTile && currentPath.path.Count > 0))
                    {
                        sailTowards();
                        if (currentPath.path.Count == 0)
                        {
                            SetAnimatorState(UnitState.Moving, false);
                            targetTile = null;
                            State = UnitState.Idle;
                        }

                    }
                    else
                    {
                        if (currentTile == targetTile && OnLandNavAgent.velocity.magnitude < 5)
                        {
                            SetAnimatorState(UnitState.Moving, false);
                            targetTile = null;
                            State = UnitState.Idle;
                        }
                        moveDirection = OnLandNavAgent.velocity.normalized;
                    }
                    TrackUnitCurrentTile();
                    updateSprite();
                    break;
                case UnitState.Patrolling:
                    if (!returnFlag)
                    {
                        int counter = 0;
                        foreach (UnitController hero in gameManager.Heroes.Values)
                        {
                            if (hero.currentTile.island == campIn.ClusterOn.island)
                            {
                                counter++;
                                float distance = Vector3.Distance(hero.transform.position, transform.position);
                                if (distance <= Unit.aggroRange && hero.currentTile.terrain == TileType.Rock)
                                {
                                    // ENTER COMBAT MODE(GATHER ALL CAMPS UNITS, PLAYER UNITS TO BATTLE)
                                    Debug.Log("PLAYER CAUGHT");
                                    gameManager.SetGameState(GameState.Combat);
                                    gameManager.GatherUnits(this, hero);
                                    break;
                                }
                            }
                        }

                        if (counter == 0)
                        {
                            targetTile = initialTile;
                            OnLandNavAgent.SetDestination(targetTile.node.worldPoint);
                            returnFlag = true;
                        }
                        else if (currentTile == targetTile && OnLandNavAgent.velocity.magnitude < 5)
                        {
                            targetTile = campIn.RandomPatrolPoint();
                            OnLandNavAgent.SetDestination(targetTile.node.worldPoint);
                        }
                    }
                    else
                    {
                        if (currentTile == targetTile && OnLandNavAgent.velocity.magnitude < 5)
                        {
                            Animator.SetBool("Moving", false);
                            returnFlag = false;
                            State = UnitState.Idle;
                        }
                    }
                    TrackUnitCurrentTile();
                    break;
            }
        }
    }

    public void CheckHealth() 
    {
        if (Unit.curHP <= 0 && State!=UnitState.Dying)
        {
            State = UnitState.Dying;
            
        }
    }

    private void StartPatrol()
    {
        Animator.SetBool("Moving", true);
        targetTile = campIn.RandomPatrolPoint();
        OnLandNavAgent.SetDestination(targetTile.node.worldPoint);
    }

    public void SetupLandNav() 
    {
        if (OnLandNavAgent == null) 
        {
            OnLandNavAgent = gameObject.AddComponent<NavMeshAgent>();
            OnLandNavAgent.speed = Speed;
            OnLandNavAgent.acceleration = Acceleration;
            OnLandNavAgent.stoppingDistance = StoppingDistance;
            OnLandNavAgent.radius = ObstacleAvoidanceRadius;
        }
        
    }


    public void DestroyLandNav() 
    {
        if (OnLandNavAgent != null) 
        {
            Component componentToRemove = gameObject.GetComponent<NavMeshAgent>();
            Destroy(componentToRemove);
            OnLandNavAgent = null;
        }
    
    }
    public void sailTowards() 
    {
        // Tile the player is moving towards
        HexTile nextPlayerTile = gameManager.grid.tiles[currentPath.path[0].gridNum];

        Vector3 targetPosition = new Vector3(nextPlayerTile.node.x, nextPlayerTile.centerHeight, nextPlayerTile.node.z);
        // Direction vector
        moveDirection = targetPosition - transform.position;
        float step = Speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

        // Turn the player (ship) to face the direction he's moving towards
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, (AngularSpeed/3) * Time.deltaTime);

        if (transform.position == targetPosition)
        {
            currentPath.path.RemoveAt(0);
        }
    }

    public void TrackUnitCurrentTile() 
    {
        RaycastHit info = gameManager.CastRay(gameManager.gameObject, gameObject);
        HexTile tileHit;
        if (info.collider != null && info.collider.gameObject.TryGetComponent<HexTile>(out tileHit))
        {
            
            if (tileHit != currentTile)
            {
                tileHit.node.occupied = true;
                currentTile.node.occupied = false;
            }
            else
            {
                currentTile.node.occupied = true;
            }
            currentTile = tileHit;
        }
    }

    public void ResetCombatFlags() 
    {
        revealInitiative = false;
        revealedInitiave = false;
        turnPlayed = false;
        prioritizeLockedCam = false;
        targetUnit = null;
        baseBoostRoll = false;
        baseBoostReveal = false;
        baseBoostShown = false;
    }

    public void updateSprite() 
    {
        if (currentTile.terrain == TileType.Water)
        {
            landSprite.SetActive(false);
            sailSprite.SetActive(true);
        }
        else
        {
            landSprite.SetActive(true);
            if (State == UnitState.Moving && !Animator.GetBool("Moving"))
            {
                SetAnimatorState(UnitState.Moving, true);
            }
            sailSprite.SetActive(false);
        }
    }

    public async Task approachTarget(Transform target)
    {
        if (!combatStartedMoving)
        {
            OnLandNavAgent.SetDestination(target.position);
            State = UnitState.Moving;
            SetAnimatorState(State, true);
            combatStartedMoving = true;
        }
        else 
        {
            await DestinationReached();
            SetAnimatorState(State, false);
            State = UnitState.AwaitingCombatTurn;
            SetAnimatorState(State, true);
            combatStartedMoving = false;
        }
    }

    public async Task DestinationReached() 
    {
        if (OnLandNavAgent.remainingDistance > 0) 
        {
            await Task.Yield();
        }
    }
}
