using UnityEngine;

public enum UnitType
{
    Hero,
    Enemy
}

public enum HeroClass
{
    Null,
    Marauder,
    Sharpshooter,
    MysticCorsair,
    Cutthroat
}

public enum EnemyClass
{
    Null,
    Mob,
    Champion,
    Boss
}


public class UnitBase: MonoBehaviour
{
    public UnitType type;
    public HeroClass heroClass;
    public EnemyClass enemyClass;

    private int lvl = 1;
    private float exp = 0;

    // Initial attribute points to spend.
    public int points = 14;

    // Directly affects the unit's max health points (maxHP).
    public int vitality = 0;
    // Directly affects the unit's speed 
    public int dexterity = 0;
    // Directly affects the character's damage output
    public int mainStat = 0;
    // Greatly affects the character's defence attribute
    public int constitution = 0;

    // Base stats, different for each unit type
    public int baseHP;
    public int baseAttack;
    public int baseDefence;
    public int baseSpeed;
    public int range;

    // Current unit instance stats
    public int curHP;
    
    // Dice rolls
    public int d20Roll;
    public int d10Roll;


    public float aggroRange;


    public void RandomlyAllocatePoints() 
    {
        while (points > 1)
        {
            int rand = UnityEngine.Random.Range(0, 4);

            switch (rand)
            {
                case 0:
                    vitality++;
                    break;
                case 1:
                    dexterity++;
                    break;
                case 2:
                    mainStat++;
                    break;
                case 3:
                    constitution++;
                    break;

            }
            points--;
        }
    }

    private void LevelUp() 
    {
        if (exp >= 1) 
        {
            // increase lvl and base attributes
            lvl++;
            baseHP+=10;
            baseAttack+=2;
            baseDefence+=2;
            baseSpeed+=3;

            // calculate maxHP and heal player
            HP();

            // allow player to increase some stats
            
            // reset exp bar;
            exp = 0;
        }
        else 
        {
            // do nothing
        }
    }

    public int HP()
    {
        return baseHP + vitality;
    }

    public int Attack() 
    {
       return d10Roll + baseAttack + mainStat;
    }

    public int Defence() 
    {
        return d10Roll + baseDefence + constitution;
    }

    /* Affects unit's turn priority when compared to other units */
    public int Speed()
    {
        return d20Roll + baseSpeed + dexterity;
    }

    public float Evasion() 
    {
        return d20Roll + (dexterity + constitution) / 2;
    }

    public float Accuracy() 
    {
        return d20Roll + mainStat;
    }


    public void applyDamage(UnitBase enemy) 
    {
        if (enemy.Evasion() > Accuracy())
        {
            // attack misses
        }
        else 
        {
            enemy.takeDamage(Attack());
        }
    }

    public void resetStats() 
    {
        this.vitality = 0;
        this.mainStat = 0;
        this.dexterity = 0;
        this.constitution = 0;
    }
    private void takeDamage(float enemyDamage) 
    {
        enemyDamage -= Defence();
        curHP = Mathf.RoundToInt(curHP - enemyDamage);

        if (curHP <= 0) 
        {
            // unit dies
        }

    }
}
