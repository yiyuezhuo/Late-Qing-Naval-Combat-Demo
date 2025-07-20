namespace StrategicCombatCore
{
    public partial class DepartmentPosition
    {
        public string objectId;
    }

    public class StaffOffice // Staff Office / 参謀本部
    {
        public DepartmentPosition chiefOfStaff = new(); // Chief of Staff / 参謀総長
        public DepartmentPosition deputyChief = new(); // Deputy chief / 参謀副長
    }

    public class ImperialJapaneseNavyGeneralStaff // Imperial Japanese Navy General Staff / 軍令部
    {
        public DepartmentPosition chiefOfTheGeneralStaff = new(); // Chief of the General Staff / 軍令部総長
    }

    public class ArmyMinistry // Army Ministry / 陸軍省
    {
        public DepartmentPosition ministersOfTheArmy = new();// Ministers of the Army / 陸軍大臣
    }

    public class MinistryOfTheNavy // Ministry of the Navy  / 海軍省
    {
        public DepartmentPosition ministersOfTheNavy = new();// Ministers of the Navy / 海軍大臣
    }

    public class ImperialGeneralHeadquarters // Imperial General Headquarters / 大本営
    {
        // https://shirakaba.link/betula/%E5%A4%A7%E6%9C%AC%E5%96%B6_(%E6%97%A5%E6%B8%85%E6%88%A6%E4%BA%89)
        public DepartmentPosition aideDeCampToTheEmperor = new(); // Aide-De-Camp to the Emperor / 侍従武官長

        // The above people (Chief of Staff, Deputy chief, ...) are included in the Imperial General Headquarters as well but it's not included them again. 

        public DepartmentPosition directorGeneralOfLogistics = new(); // Director-General of Logistics / 兵站総監
    }


    public class JapaneseHighCommand
    {
        public StaffOffice staffOffice = new();
        public ImperialJapaneseNavyGeneralStaff imperialJapaneseNavyGeneralStaff = new();
        public ArmyMinistry armyMinistry = new();
        public MinistryOfTheNavy ministryOfTheNavy = new();
        public ImperialGeneralHeadquarters imperialGeneralHeadquarters = new();
    }

    public class BeiyangLocal // Beiyang / 北洋
    {
        public DepartmentPosition beiyangMinister = new(); // Superintendent of Trade for the Northern Ports  / 北洋通商大臣
        public DepartmentPosition viceroyOfZhili = new(); // Viceroy of Zhili / 直隶总督 
        public DepartmentPosition generalOfFengTian = new(); // General of Fengtian / 奉天将军
        public DepartmentPosition governorOfShandong = new(); // Governor of Shandong / 山东巡抚
        public DepartmentPosition admiralOfTheBeiyangFleet = new(); // Admiral of the Beiyang Fleet / 北洋水师提督
    }

    public class MinistryOfWarChinese // Ministry of War / 兵部
    {
        public DepartmentPosition ministerOfWarManchu = new(); // Minister of War (Manchu) / 兵部尚书 (满缺)
        public DepartmentPosition ministerOfWarHan = new(); // Minister of War (Han) / 兵部尚书 (汉缺)
    }

    public class NavalMinistryChinese // Naval Ministry / 总理海军事务衙门
    {
        public DepartmentPosition grandMinisterOfTheNavy = new(); // Grand Minister of the Navy / 总理大臣
        public DepartmentPosition associateMinisterOfTheNavy = new(); // Associate Minister of the Navy / 会办大臣
        public DepartmentPosition assistantMinisterOfTheNavy = new(); // Assistant Minister of the Navy / 帮办大臣
    }

    public class ChineseHighCommand
    {
        public BeiyangLocal beiyangLocal = new();
        public MinistryOfWarChinese ministryOfWar = new();
        public NavalMinistryChinese navalMinistry = new();
    }

    public class HighCommand
    {
        public JapaneseHighCommand japaneseHighCommand = new();
        public ChineseHighCommand chineseHighCommand = new();
    }
}