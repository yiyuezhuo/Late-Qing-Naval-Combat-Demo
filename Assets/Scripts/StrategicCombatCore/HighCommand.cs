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

    }


    public class JapaneseHighCommand
    {
        public StaffOffice staffOffice = new();
        public ImperialJapaneseNavyGeneralStaff imperialJapaneseNavyGeneralStaff = new();
        public ArmyMinistry armyMinistry = new();
        public MinistryOfTheNavy ministryOfTheNavy = new();
        public ImperialGeneralHeadquarters imperialGeneralHeadquarters = new();
    }

    public class ChineseHighCommand
    {

    }

    public class HighCommand
    {
        public JapaneseHighCommand japaneseHighCommand = new();
        public ChineseHighCommand chineseHighCommand = new();
    }
}