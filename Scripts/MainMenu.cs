using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public enum Stats 
{
    Vitality,
    MainStat,
    Dexterity,
    Constitution
}


[System.Serializable]
public class SerializableCharacter
{
    public HeroClass hclass;
    public int vitality;
    public int mainStat;
    public int dexterity;
    public int constitution;

    public SerializableCharacter(HeroClass hclass, int vitality, int mainStat, int dexterity, int constitution)
    {
        this.hclass = hclass;
        this.vitality = vitality;
        this.mainStat = mainStat;
        this.dexterity = dexterity;
        this.constitution = constitution;
    }
}



public class MainMenu : MonoBehaviour
{
    public List<SerializableCharacter> characterList = new List<SerializableCharacter>();

    Scene mainMenu;
    Scene newGame;

    public int skillPoints = 14;

    public GameObject ClassView;
    int classIndex;

    public GameObject StatNames;
    TextMeshProUGUI statNameValue;

    public GameObject Marauder;
    UnitBase unit0;
    public GameObject Sharpshooter;
    UnitBase unit1;
    public GameObject MysticCorsair;
    UnitBase unit2;
    public GameObject Cutthroat;
    UnitBase unit3;
    UnitBase unitDisplayed;
    const int maxHeroes = 2;

    
    public GameObject newGamebutton;
    public GameObject quitGamebutton;

    public GameObject CharCreationWindow;


    [Header("Hero Stats")]
    public GameObject ClassName;
    TextMeshProUGUI classNameValue;

    public GameObject SkillPoints;
    TextMeshProUGUI skillPointsValue;

    public GameObject Vitality;
    TextMeshProUGUI vitalityValue;
    
    public GameObject MainStat;
    TextMeshProUGUI mainStatValue;

    public GameObject Dexterity;
    TextMeshProUGUI dexterityValue;

    public GameObject Constitution;
    TextMeshProUGUI constitutionValue;

    public GameObject BaseHP;
    TextMeshProUGUI baseHPValue;

    public GameObject BaseAttack;
    TextMeshProUGUI baseAttackValue;
    
    public GameObject BaseDefence;
    TextMeshProUGUI baseDefenceValue;
    
    public GameObject BaseSpeed;
    TextMeshProUGUI baseSpeedValue;

    public GameObject CurrentHP;
    TextMeshProUGUI currentHPValue;

    public GameObject CurrentAttack;
    TextMeshProUGUI currentAttackValue;

    public GameObject CurrentDefence;
    TextMeshProUGUI currentDefenceValue;

    public GameObject CurrentSpeed;
    TextMeshProUGUI currentSpeedValue;

    public GameObject nextClass;
    public GameObject prevClass;

    /* Buttons */
    [Header("Stat Buttons")]
    public UnityEngine.UI.Button addVitality;
    public UnityEngine.UI.Button subVitality;
    public UnityEngine.UI.Button addMainStat;
    public UnityEngine.UI.Button subMainStat;
    public UnityEngine.UI.Button addDexterity;
    public UnityEngine.UI.Button subDexterity;
    public UnityEngine.UI.Button addConstitution;
    public UnityEngine.UI.Button subConstitution;
    
    public UnityEngine.UI.Button finalizeHero;
    public UnityEngine.UI.Button exitCharacterCreation;


    /* Alert Window */
    [Header("Alert Window References")]
    public GameObject PromptWindow;
    public GameObject PromptPanel;
    TextMeshProUGUI promptValue;

    public UnityEngine.UI.Button LButton;
    TextMeshProUGUI lButtonValue;
    public UnityEngine.UI.Button RButton;
    TextMeshProUGUI rButtonValue;


    

    private void Awake()
    {
        setupButtons();
        ToggleCreation(false);
        mainMenu = SceneManager.GetActiveScene();
    }

    private void NewCharacter() 
    {
        SerializableCharacter Hero = new SerializableCharacter(unitDisplayed.heroClass,
            unitDisplayed.vitality, unitDisplayed.mainStat, unitDisplayed.dexterity, unitDisplayed.constitution);
        characterList.Add(Hero);
    }

    private void ClearStats() 
    {
        unit0.resetStats();
        unit1.resetStats();
        unit2.resetStats();
        unit3.resetStats();

        skillPoints = 14;
    }

    void GrowFleet() 
    {
        PromptWindow.SetActive(false);
        ToggleCreation(true);
        UpdateUI();
    }

    public void MakeEmWalkThePlank() 
    {
        PromptWindow.SetActive(false);
        ToggleCreation(false);
        characterList = new List<SerializableCharacter>();
    }

    public void NewGame()
    {
        for(int i = 0; i < characterList.Count; i++) 
        {
            string hero = $"Hero {i+1}";
            string json = JsonUtility.ToJson(characterList[i]);
            PlayerPrefs.SetString(hero, json);
        }
        SceneManager.LoadScene(1);
        newGame = SceneManager.GetSceneAt(1);
    }

    public void finalizeCharacter() 
    {
        if(skillPoints == 0) 
        {
            // save player's choice
            NewCharacter();
            if (characterList.Count < maxHeroes)
            {
                DisplayPrompt($"Ye can add {maxHeroes - characterList.Count} additional Pirate(s) t' yer fleet.",
                    "Add another Pirate", GrowFleet,
                    "Start Game", NewGame);
            }
            else 
            {
                DisplayPrompt($"Ye be ready t' start yer journey!\nAre ye sure ye wants t' continue wit' this fleet?",
                    "Aye!", NewGame, 
                    "Make 'em walk the Plank", MakeEmWalkThePlank);
            }
        }
    }

    private void setupButtons() 
    {
        unit0 = Marauder.GetComponent<UnitBase>();
        unit1 = Sharpshooter.GetComponent<UnitBase>();
        unit2 = MysticCorsair.GetComponent<UnitBase>();
        unit3 = Cutthroat.GetComponent<UnitBase>();

        vitalityValue = Vitality.GetComponent<TextMeshProUGUI>();
        mainStatValue = MainStat.GetComponent<TextMeshProUGUI>();
        dexterityValue = Dexterity.GetComponent<TextMeshProUGUI>();
        constitutionValue = Constitution.GetComponent<TextMeshProUGUI>();
        skillPointsValue = SkillPoints.GetComponent<TextMeshProUGUI>();
        classNameValue = ClassName.GetComponent<TextMeshProUGUI>();
        baseHPValue = BaseHP.GetComponent<TextMeshProUGUI>();
        baseAttackValue = BaseAttack.GetComponent<TextMeshProUGUI>();
        baseDefenceValue = BaseDefence.GetComponent<TextMeshProUGUI>();
        baseSpeedValue = BaseSpeed.GetComponent<TextMeshProUGUI>();
        currentHPValue = CurrentHP.GetComponent<TextMeshProUGUI>();
        currentAttackValue = CurrentAttack.GetComponent<TextMeshProUGUI>();
        currentDefenceValue = CurrentDefence.GetComponent<TextMeshProUGUI>();
        currentSpeedValue = CurrentSpeed.GetComponent<TextMeshProUGUI>();
        promptValue = PromptPanel.GetComponent<TextMeshProUGUI>();
        lButtonValue = LButton.GetComponentInChildren <TextMeshProUGUI>();
        rButtonValue = RButton.GetComponentInChildren<TextMeshProUGUI>();
        statNameValue = StatNames.GetComponent<TextMeshProUGUI>();

    }



    public void incrementClassIndex() 
    {
        classIndex = classIndex == 3 ? 3 : classIndex + 1;
        nextClass.SetActive(classIndex == 3 ? false : true);
        prevClass.SetActive(true);
        switchClass();
    }

    public void reduceClassIndex() 
    {
        classIndex = classIndex == 0 ? 0 : classIndex - 1;
        nextClass.SetActive(true);
        prevClass.SetActive(classIndex == 0 ? false : true);
        switchClass();
    }

    public void switchClass() 
    {
        string stat = "";
        
        switch (classIndex) 
        {
            case 0:
                unitDisplayed = unit0;
                Marauder.SetActive(true);
                Sharpshooter.SetActive(false);
                MysticCorsair.SetActive(false);
                Cutthroat.SetActive(false);
                stat = "Strength";
                break;
                
            case 1:
                unitDisplayed = unit1;
                Marauder.SetActive(false);
                Sharpshooter.SetActive(true);
                MysticCorsair.SetActive(false);
                Cutthroat.SetActive(false);
                stat = "Precision";
                break;

            case 2:
                unitDisplayed = unit2;
                Marauder.SetActive(false);
                Sharpshooter.SetActive(false);
                MysticCorsair.SetActive(true);
                Cutthroat.SetActive(false);
                stat = "Intelligence";
                break;
                
            case 3:
                unitDisplayed = unit3;
                Marauder.SetActive(false);
                Sharpshooter.SetActive(false);
                MysticCorsair.SetActive(false);
                Cutthroat.SetActive(true);
                stat = "Nimbleness";
                break;
        }

        statNameValue.text = $"Vitality\n{stat}\nDexterity\nConstitution";
        ClearStats();
        UpdateUI();
    }

    public void AddStat(int stat) 
    {
        switch (stat)
        {
            case 0:
                unitDisplayed.vitality += 1;
                break;
            case 1:
                unitDisplayed.mainStat += 1;
                break;
            case 2:
                unitDisplayed.dexterity += 1;
                break;
            case 3:
                unitDisplayed.constitution += 1;
                break;
   
        }
        skillPoints -= 1;
        UpdateUI();
    }

    public void DisplayPrompt (string msg, 
        string lButton, 
        UnityEngine.Events.UnityAction lAction, 
        string rButton, 
        UnityEngine.Events.UnityAction rAction) 
    {
        LButton.onClick.RemoveAllListeners();
        RButton.onClick.RemoveAllListeners();

        promptValue.text = msg;
        lButtonValue.text = lButton;
        rButtonValue.text = rButton;
        LButton.onClick.AddListener(lAction);
        RButton.onClick.AddListener(rAction);
        PromptWindow.SetActive(true);
    }

    public void UpdateUI()
    {
        vitalityValue.text = unitDisplayed.vitality.ToString();
        mainStatValue.text = unitDisplayed.mainStat.ToString();
        dexterityValue.text = unitDisplayed.dexterity.ToString();
        constitutionValue.text = unitDisplayed.constitution.ToString();
        skillPointsValue.text = skillPoints.ToString();

        baseHPValue.text = unitDisplayed.baseHP.ToString();
        baseAttackValue.text = unitDisplayed.baseAttack.ToString();
        baseDefenceValue.text = unitDisplayed.baseDefence.ToString();
        baseSpeedValue.text = unitDisplayed.baseSpeed.ToString();

        currentHPValue.text = unitDisplayed.HP().ToString();
        currentAttackValue.text = unitDisplayed.Attack().ToString();
        currentDefenceValue.text = unitDisplayed.Defence().ToString();
        currentSpeedValue.text = unitDisplayed.Speed().ToString();

        addVitality.gameObject.SetActive(skillPoints == 0 ? false : true);
        addMainStat.gameObject.SetActive(skillPoints == 0 ? false : true);
        addDexterity.gameObject.SetActive(skillPoints == 0 ? false : true);
        addConstitution.gameObject.SetActive(skillPoints == 0 ? false : true);
        subVitality.gameObject.SetActive(unitDisplayed.vitality == 0 ? false : true);
        subMainStat.gameObject.SetActive(unitDisplayed.mainStat == 0 ? false : true);
        subDexterity.gameObject.SetActive(unitDisplayed.dexterity == 0 ? false : true);
        subConstitution.gameObject.SetActive(unitDisplayed.constitution == 0 ? false : true);

        classNameValue.text = unitDisplayed.heroClass.ToString();

    }

    public void SubStat(int stat)
    {
        switch (stat)
        {
            case 0:
                unitDisplayed.vitality = unitDisplayed.vitality == 0 ? 0: unitDisplayed.vitality - 1;
                break;
            case 1:
                unitDisplayed.mainStat = unitDisplayed.mainStat == 0 ? 0 : unitDisplayed.mainStat - 1;
                break;
            case 2:
                unitDisplayed.dexterity = unitDisplayed.dexterity == 0 ? 0: unitDisplayed.dexterity - 1;
                break;
            case 3:
                unitDisplayed.constitution = unitDisplayed.constitution == 0 ? 0 : unitDisplayed.constitution - 1;
                break;
        }
        skillPoints += 1;
        UpdateUI();
        
    }

    public void Quit() 
    {
        Application.Quit();
    }

    public void ToggleCreation(bool flag)
    {
        quitGamebutton.SetActive(!flag);
        newGamebutton.SetActive(!flag);
        CharCreationWindow.SetActive(flag);
        ClearStats();
        unitDisplayed = unit0;
        classIndex = 0;
        switchClass();
    }
}
