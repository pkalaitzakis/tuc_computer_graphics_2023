using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCamp : MonoBehaviour
{
    GameManager gameManager;
    RockCluster clusterOn;
    bool bossSpawned;
    public List<UnitController> enemies;

    public RockCluster ClusterOn { get { return clusterOn; } }
    public Dictionary<UnitController, EnemyCamp> Spawn(GameManager manager, RockCluster cluster, string campName) 
    {
        this.gameManager = manager;
        this.clusterOn = cluster;
        this.gameObject.name = campName;
        return SpawnEnemiesInCamp();
    }

    public Dictionary<UnitController, EnemyCamp> SpawnEnemiesInCamp()
    {
        enemies = new List<UnitController> ();
        Transform spawnPoints = gameObject.transform.GetChild(0);
        Dictionary<UnitController, EnemyCamp> enemyUnits = new Dictionary<UnitController, EnemyCamp>();
        int randEnemyClassIndex;
        GameObject newEnemy;
        UnitController enemyController;
        Transform spawnPoint;
        int enemiesSpawned = 0;
        int enemiesInCamp = 3;


        while (enemiesSpawned < enemiesInCamp)
        {
            if (bossSpawned)
            {
                randEnemyClassIndex = Random.Range(1, gameManager.EnemyClasses.Count);
            }
            else
            {
                randEnemyClassIndex = Random.Range(0, gameManager.EnemyClasses.Count);
            }

            bossSpawned = randEnemyClassIndex == 0 ? true : false;
            int slotIndex = 0;

            while (true)
            {
                spawnPoint = spawnPoints.transform.GetChild(slotIndex);
                if (spawnPoint.transform.childCount != 0)
                {
                    slotIndex++;
                }
                else
                {
                    break;
                }
            }

            newEnemy = Instantiate(gameManager.EnemyClasses[randEnemyClassIndex], spawnPoint.position, spawnPoint.rotation, spawnPoint);
            enemyController = newEnemy.GetComponent<UnitController>();
            enemyController.InitEnemy(gameManager, this);
            enemyUnits.Add(enemyController, this);
            enemies.Add(enemyController);
            enemiesSpawned++;
        }

        return enemyUnits;
    }


    public HexTile RandomPatrolPoint()
    {
        while (true)
        {
            int campTileIndex = UnityEngine.Random.Range(0, ClusterOn.rockTiles.Count);
            HexTile targetCampTile = ClusterOn.rockTiles[campTileIndex];
            
            if (!targetCampTile.node.occupied)
                return targetCampTile;
        }

    }

}
