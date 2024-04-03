using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using UnityEngine.AI;
using System;

public enum GameState
{
    Explore,
    Combat,
    GameClear,
    GameOver
}

public class GameManager : MonoBehaviour
{
    public GameManager instance;
    public LayerMask userInterface;
    public LayerMask tileMask;
    public LayerMask inputMask;
    public LayerMask heroMask;
    public LayerMask enemyMask;
    public bool turnComplete;
    
    /* GAMEPLAY VARIABLES */
    GameState gameState = GameState.Explore;
    List<UnitController> unitsInCombat;
    List<Task> diceRolls;
    List<Task> baseBoostRolls;
    public bool damageApplied = false;
    // Combat Mode flags
    public bool turnStart = true;
    public bool unitsRolled = false;
    public bool diceShown = false;
    public bool setUnitActionState = false;
    public bool setUnitActionComplete = false;

    /* CAMERA SYSTEM VARIABLES */
    [Header("Camera")]
    public GameObject CamRig;
    public Camera MainCamera;
    public CameraRigController CamRigController;
    public Vector3 camRigCurPos = new Vector3();

    /* GAME WORLD VARIABLES */
    public HexGrid grid;
    public Dictionary<int, UnitController> Heroes;
    public List<Dictionary<UnitController, EnemyCamp>> Camps;
    public int maxEnemyCamps = 2;
    public int maxEnemiesInCamp = 3;

    [Header("Enemy Class Prefabs")]
    public List<GameObject> EnemyClasses = new List<GameObject>();

    [Header("Hero Class Prefabs")]
    public GameObject MarauderClass;
    public GameObject SharpshooterClass;
    public GameObject MysticCorsairClass;
    public GameObject CutthroatClass;

    /* USER INTERFACE VARIABLES */

    [Header("Hero Sprites")]
    public Sprite MarauderSprite;
    public Sprite SharpshooterSprite;
    public Sprite MysticCorsairSprite;
    public Sprite CutthroatSprite;

    [Header("UI Panel")]
    public const int maxHeroes = 2;
    public List<GameObject> HeroPanel = new List<GameObject>();
    Dictionary<Slider, UnitBase> HeroHealthBars = new Dictionary<Slider, UnitBase>();

   
    public int activeHeroIndex;
    public GameObject highlightedObj;


    void Awake()
    {
        // Ensure singleton class instance
        instance = this;

        // Initialize mask refferences once
        inputMask = LayerMask.GetMask("UI", "Water", "Sand", "Grass", "Ground", "Rock", "Highlight");
        tileMask = LayerMask.GetMask("Water", "Sand", "Grass", "Ground", "Rock");
        userInterface = LayerMask.NameToLayer("UI");
        enemyMask = LayerMask.NameToLayer("Enemy");
        heroMask = LayerMask.NameToLayer("Hero");

        // Create new board game object
        GameObject board = new GameObject("GameBoard");

        // Awake world generation
        grid = board.AddComponent<HexGrid>().SpawnWorld(instance);

        // Awake heroes
        SpawnHeroes();

        // Awake enemies in camps
        SpawnEnemyCamps();

        // Awake cam system
        CamRigController = CamRig.GetComponent<CameraRigController>().SetupCameraRig(this);
        SelectHero(0);
    }



    void SpawnHeroes()
    {
        GameObject heroObj;
        Heroes = new Dictionary<int, UnitController>();
        for (int i = 0; i < maxHeroes; i++)
        {
            string pref = PlayerPrefs.GetString($"Hero {i + 1}");

            if (pref != "")
            {
                SerializableCharacter Hero = JsonUtility.FromJson<SerializableCharacter>(pref);

                switch (Hero.hclass)
                {
                    case HeroClass.Sharpshooter:
                        heroObj = GameObject.Instantiate(SharpshooterClass);
                        break;
                    case HeroClass.MysticCorsair:
                        heroObj = GameObject.Instantiate(MysticCorsairClass);
                        break;
                    case HeroClass.Cutthroat:
                        heroObj = GameObject.Instantiate(CutthroatClass);
                        break;
                    default:
                        heroObj = GameObject.Instantiate(MarauderClass);
                        break;
                }

                UnitBase unit = heroObj.GetComponent<UnitBase>();
                unit.vitality = Hero.vitality;
                unit.mainStat = Hero.mainStat;
                unit.dexterity = Hero.dexterity;
                unit.constitution = Hero.constitution;
                unit.curHP = unit.HP();
                unit.points = 0;

                UnitController heroController = heroObj.GetComponent<UnitController>();
                heroController.InitHero(instance);
                Heroes.Add(i, heroController);
                SetupUI(HeroPanel[i], heroObj);
            }
        }
    }

    void SpawnEnemyCamps()
    {
        Camps = new List<Dictionary<UnitController, EnemyCamp>>();
        List<RockCluster> choosenClusters = PickRandomClusters(grid.clusters, maxEnemyCamps);
        int instantiatedCamps = 0;

        foreach (RockCluster cluster in choosenClusters)
        {
            cluster.CenterTile();
            // Destroy previous environment object
            cluster.centerTile.envObj.SetActive(false);
            Destroy(cluster.centerTile.envObj);

            // Instantiate new Enemy Base Game object
            GameObject camp = Instantiate(grid.rockPrefabs[grid.rockPrefabs.Count - 1],
                cluster.centerTile.node.worldPoint,
                Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));

            EnemyCamp campComponent = camp.AddComponent<EnemyCamp>();
            Camps.Add(campComponent.Spawn(instance, cluster, $"Enemy Base {instantiatedCamps}"));
            cluster.centerTile.enemyCamp = true;
            instantiatedCamps++;
        }
    }

    public GameState GetGameState() { return gameState; }
    public void SetGameState(GameState state) { this.gameState = state; }

    public RaycastHit CastRay(GameObject from, GameObject to)
    {
        Vector3 originPosition = from.transform.position; // Get the position of the origin GameObject
        Vector3 targetPosition = to.transform.position; // Get the position of the target GameObject

        Vector3 direction = targetPosition - originPosition; // Calculate the direction from origin to target
        Ray ray = new Ray(originPosition, direction); // Create a ray from the origin position and direction
        RaycastHit hit;
        Physics.Raycast(ray, out hit, float.MaxValue, tileMask);

        Debug.DrawRay(ray.origin, float.MaxValue*ray.direction, Color.red, 1f);

        return hit;
    }

    

    public async void Update()
    {
        GetInput();
        UpdateUI();

        if (gameState == GameState.Combat) 
        {
            if (turnStart)
            {
                if (!unitsRolled)
                {
                    RollD20s();
                }
                else if (!diceShown)
                {
                    ShowRolls(unitsInCombat);
                }
                else
                {
                    SetInitiatives();
                    turnStart = false;
                }
            }
            else if (!turnComplete)
            {
                PlayTurn();
            }
            else 
            {
                diceRolls = null;
                baseBoostRolls = null;
                damageApplied = false;
                turnStart = true;
                unitsRolled = false;
                diceShown = false;
                setUnitActionState = false;
                setUnitActionComplete = false;
                turnComplete = false;
                
            }
            
        }
    }

    public async void RollD20s()
    {
        if (diceRolls==null) 
        {
            diceRolls = new List<Task>();
            foreach (UnitController unit in unitsInCombat)
            {
                unit.diceRoller.Throw(0, unit);
                Debug.Log($"{unit.gameObject.name} rolled");
                diceRolls.Add(unit.diceRoller.Shuffle());
            }
        }
        Debug.Log("Awaiting roll");
        await Task.WhenAll(diceRolls);
        Debug.Log("All dice rolled");
        unitsRolled = true;
    }

    public async void ShowRolls(List<UnitController> units) 
    {
        foreach (UnitController unit in units) 
        {
            if (!unit.revealInitiative) 
            {
                unit.DiceCam.Priority = 10;
                unit.diceRoller.showInterval = DateTime.Now.AddSeconds(5);
                unit.revealInitiative = true;
                Debug.Log($"{unit.gameObject.name} showing his roll");
            }
            Debug.Log("Waiting");
            await unit.diceRoller.ShowDice();
            if (!unit.revealedInitiave) 
            {
                Debug.Log($"{unit.gameObject.name} done, switching cameras");
                unit.revealedInitiave = true;
                unit.DiceCam.Priority = 1;
            }
        }
        if (!diceShown) 
        {
            Debug.Log("All dice shown");
            diceShown = true;
        }
    }

    public void SetInitiatives()
    {
        unitsInCombat.Sort((unit1, unit2) => unit2.Unit.Speed().CompareTo(unit1.Unit.Speed())); // Sort in descending order
    }

    public async void PlayTurn() 
    {
        foreach (UnitController unitInCombat in unitsInCombat) 
        {
            if (!unitInCombat.turnPlayed) 
            {
                if (unitInCombat.Unit.heroClass != HeroClass.Null)
                {
                    await Task.Delay(5000);
                    unitInCombat.turnPlayed = true;

                }
                else
                {              
                    await AttackLeastHealthyHero(unitInCombat);
                    unitInCombat.turnPlayed = true;
                }
            }
        }
        turnComplete = true;
    }

    public async Task AttackLeastHealthyHero(UnitController enemy) 
    {
        if (enemy.targetUnit == null) 
        {
            // deep copy heroes list
            List<UnitController> heroesAlive = new List<UnitController>(Heroes.Values);

            // sort deep copy based on unit's life
            heroesAlive.Sort((hero1, hero2) => hero1.Unit.curHP.CompareTo(hero2.Unit.curHP));

            // target the least healthy hero?
            enemy.targetUnit = heroesAlive[0];
        }
        await ExecuteMainAction(enemy, enemy.targetUnit);
    }

    public async Task ExecuteMainAction(UnitController attacker, UnitController defender) 
    {
        if (!attacker.baseBoostRoll && !defender.baseBoostRoll && baseBoostRolls == null) 
        {
            baseBoostRolls = new List<Task>();
            CamRigController.BattleCam.Priority = 10;

            attacker.diceRoller.Throw(0, attacker);
            attacker.diceRoller.Throw(1, attacker);
            baseBoostRolls.Add(attacker.diceRoller.Shuffle());
            attacker.baseBoostRoll = true;
            defender.diceRoller.Throw(0, defender);
            defender.diceRoller.Throw(1, defender);
            baseBoostRolls.Add(defender.diceRoller.Shuffle());
            defender.baseBoostRoll = true;   
        }

        await Task.WhenAll(baseBoostRolls);

        if (!attacker.baseBoostReveal) 
        {
            CamRigController.BattleCam.Priority = 1;
            attacker.DiceCam.Priority = 10;
            attacker.diceRoller.showInterval = DateTime.Now.AddSeconds(5);
            attacker.baseBoostReveal = true;
        }

        await attacker.diceRoller.ShowDice();

        if (!attacker.baseBoostShown) 
        {
            attacker.DiceCam.Priority = 1;
            attacker.baseBoostShown = true;
        }

        if (!defender.baseBoostReveal)
        {
            CamRigController.BattleCam.Priority = 1;
            defender.DiceCam.Priority = 10;
            defender.diceRoller.showInterval = DateTime.Now.AddSeconds(5);
            defender.baseBoostReveal = true;
        }

        await defender.diceRoller.ShowDice();

        if (!defender.baseBoostShown)
        {
            defender.DiceCam.Priority = 1;
            defender.baseBoostShown = true;
        }

        if (!setUnitActionState) 
        {
            CamRigController.BattleCam.Priority = 10;
            attacker.State = UnitState.Attacking;
            if (attacker.Unit.Accuracy() >= defender.Unit.Evasion())
            {
                defender.State = UnitState.GettingHit;
            }
            else
            {
                defender.State = UnitState.Evading;
            }
            attacker.SetAnimatorState(attacker.State, true);
            defender.SetAnimatorState(defender.State, true);
            setUnitActionState = true;
        }

        if (!damageApplied)
        {
            attacker.Unit.applyDamage(defender.Unit);
            damageApplied = true;
        }

        if (attacker.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && defender.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            await Task.Yield();
        }

        if (!setUnitActionComplete)
        {
            setUnitActionComplete = true;
            attacker.SetAnimatorState(attacker.State, false);
            attacker.State = UnitState.AwaitingCombatTurn;
            defender.SetAnimatorState(defender.State, false);
            defender.State = UnitState.AwaitingCombatTurn;
        }

    }
    public void GetInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = MainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, inputMask))
            {
                int layer = hit.collider.gameObject.layer;
                HexTile tileHit;
                if (layer == userInterface)
                {
                    SelectHero(hit.collider.gameObject.name == "Slot0" ? 0 : 1);
                }
                else if(hit.collider.gameObject.TryGetComponent<HexTile>(out tileHit) && tileHit.highlighted)
                { 
                    if (tileHit.highlighted)
                    {
                        Heroes[activeHeroIndex].targetTile = tileHit;
                        UnhighlightTile(tileHit);
                    }
                }
            }
        }
    }


    public void HighlightTile(HexTile tile) 
    {
        UnitController activeHeroController = Heroes[activeHeroIndex];
        if (activeHeroController.State == UnitState.Idle && !tile.node.occupied)
        {
            if (activeHeroController.currentTile.terrain == TileType.Water)
            {
                if (tile.terrain == TileType.Water)
                {
                    tile.renderer.materials = tile.highlightedList;
                    tile.highlighted = true;
                    highlightedObj = tile.gameObject;
                }
                else if (tile.dockTile)
                {
                    tile.gameObject.layer = LayerMask.NameToLayer("Highlight");
                    tile.highlighted = true;
                    highlightedObj = tile.island.gameObject;
                }
            }
            else
            {
                if (!activeHeroController.currentTile.dockTile && tile.terrain == TileType.Water)
                {
                    // ignoreHover
                }
                else
                {
                    if (tile.terrain == TileType.Water)
                    {
                        tile.renderer.materials = tile.highlightedList;
                    }
                    else 
                    {
                        tile.gameObject.layer = LayerMask.NameToLayer("Highlight");
                    }
                    highlightedObj = tile.gameObject;
                    tile.highlighted = true;
                }
            }    
        }
    }

    public void UnhighlightTile(HexTile tile) 
    {
        if (tile.terrain == TileType.Water)
        {
            tile.renderer.materials = tile.materialList;
        }
        else
        {
            tile.gameObject.layer = LayerMask.NameToLayer(tile.terrain.ToString());
        }
        tile.highlighted = false;
        highlightedObj = null;
    }

    public void GatherUnits(UnitController enemy, UnitController heroCaught) 
    {
        unitsInCombat = new List<UnitController>();
        foreach (UnitController hero in Heroes.Values) 
        {
            if (hero != heroCaught)
            {
                bool[,] visited = new bool[grid.gridDimension.x, grid.gridDimension.y];
                HexNode targetNode = grid.RandomTileAtDepth(heroCaught.currentTile, 0, 3, visited).node;
                hero.transform.position = targetNode.worldPoint;
                hero.SetupLandNav();
                hero.OnLandNavAgent.Warp(targetNode.worldPoint);
                hero.TrackUnitCurrentTile();
                hero.updateSprite();

            }
            else if(hero.OnLandNavAgent.velocity.magnitude>0) 
            {
                hero.targetTile = null;
                hero.OnLandNavAgent.velocity = Vector3.zero;
                hero.OnLandNavAgent.isStopped = true;
            }
            unitsInCombat.Add(hero);
            hero.SetAnimatorState(hero.State, false);
            
            hero.State = UnitState.AwaitingCombatTurn;
            hero.Animator.SetBool("Moving", false);
            hero.Animator.SetBool("AwaitingCombatTurn", true);
            hero.transform.LookAt(enemy.transform);
        }

        foreach (UnitController enemyController in enemy.campIn.enemies) 
        {
            // Freeze all enemies in their track
            if (enemyController.OnLandNavAgent.velocity.magnitude > 0)
            {
                enemyController.targetTile = null;
                enemyController.OnLandNavAgent.velocity = Vector3.zero;
                enemyController.OnLandNavAgent.isStopped = true;
            }

            if (enemyController != enemy) 
            {
                bool[,] visited = new bool[grid.gridDimension.x, grid.gridDimension.y];
                HexNode targetNode;

                if (enemyController.Unit.enemyClass == EnemyClass.Boss)
                {
                    targetNode = grid.RandomTileAtDepth(heroCaught.currentTile, 0, 3, visited).node;
                }
                else if (enemyController.Unit.enemyClass == EnemyClass.Champion) 
                {
                    targetNode = grid.RandomTileAtDepth(heroCaught.currentTile, 0, 2, visited).node;
                }
                else 
                {
                    targetNode = grid.RandomTileAtDepth(heroCaught.currentTile, 0, 1, visited).node;
                }
                enemyController.OnLandNavAgent.Warp(targetNode.worldPoint);
                enemyController.TrackUnitCurrentTile();
            }
            enemyController.transform.LookAt(heroCaught.transform);
            unitsInCombat.Add(enemyController);
            enemyController.State = UnitState.AwaitingCombatTurn;
            enemyController.Animator.SetBool("Moving", false);
            enemyController.Animator.SetBool("AwaitingCombatTurn", true);
        }
        CamRigController.SetupBattleCam(unitsInCombat);
    }

    public void checkGameOver() 
    {
        foreach (UnitController hero in Heroes.Values) 
        {
            if (hero.Unit.curHP <= 0) 
            {

            }
        }
    }

    public void SetupUI(GameObject slot, GameObject heroObj)
    {
        GameObject ImagePanel = slot.transform.Find("ClassSprite").gameObject;
        switch (heroObj.GetComponent<UnitBase>().heroClass)
        {
            case HeroClass.Marauder:
                ImagePanel.GetComponent<Image>().sprite = MarauderSprite;
                break;
            case HeroClass.Sharpshooter:
                ImagePanel.GetComponent<Image>().sprite = SharpshooterSprite;
                break;
            case HeroClass.MysticCorsair:
                ImagePanel.GetComponent<Image>().sprite = MysticCorsairSprite;
                break;
            case HeroClass.Cutthroat:
                ImagePanel.GetComponent<Image>().sprite = CutthroatSprite;
                break;
        }
        Slider health = slot.transform.Find("HealthBar").gameObject.GetComponent<Slider>();
        health.value = heroObj.GetComponent<UnitBase>().HP();
        HeroHealthBars.Add(health, heroObj.GetComponent<UnitBase>());
        gameObject.SetActive(true);
    }

    public void SelectHero(int slotIndex)
    {
        GameObject selectedHero = HeroHealthBars.Values.ElementAt(slotIndex).gameObject;
        CamRigController.LockOnUnit(selectedHero);
        activeHeroIndex = slotIndex;
    }

    private void UpdateUI()
    {
        foreach (KeyValuePair<Slider, UnitBase> slot in HeroHealthBars)
        {
            slot.Key.value = (float)slot.Value.curHP / slot.Value.HP();
        }

    }



    public List<RockCluster> PickRandomClusters(List<RockCluster> clusters, int n)
    {
        int largestClusterIndex = clusters.Count - 1;
        List<RockCluster> clusterPool = new List<RockCluster>();

        for (int i = 0; i < maxEnemyCamps; i++)
        {
            clusterPool.Add(clusters[largestClusterIndex - i]);
        }
        System.Random random = new System.Random();

        List<RockCluster> choosenClusters = clusterPool.OrderBy(x => random.Next()).Take(n).ToList();

        return choosenClusters;
    }
    


}
