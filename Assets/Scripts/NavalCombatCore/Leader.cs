using System.Collections.Generic;

namespace NavalCombatCore
{
    public enum LeaderSkillLevel
    {
        Unknown, // Placeholder, but if it's used in the actual game, the value is determined by rolling a die, either based on lower-level abilities (with an informative prior) or completely at random (with a non-informative prior)
        BarelyCompetent,
        Average,
        AboveAverage,
        Outstanding,
        Gifted,
    }

    public enum LeaderTrait
    {
        NotSpecified,
        Coward, // Assigned to leader who flee or retreat unexpected. (Liu Buchan)
        Brave, // Assigned to leader who commit suicide attack (Deng Shichang)
        ExperiencedCaptain, // Leader may get the trait if its ship / division has a active role in the battle.
        ExperiencedTactician, // Leader may get the trait if it command high-level organization in a fleet in a combat.
        ExperiencedStrategist, // Leader may get the trait if it generate a combat in a theater (average probability) and barely hold planning position in a intense theater (low-probability, Fleet in being, Fabian).
        ExperiencedNavalBureaucrat, // Leader may get the trait when it has role in the high command, general staff or work on budget decision and competition.
        NotFromNavalBackground, // Assigned to Ding Ruchang
    }

    public class Leader : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public string portraitCode;
        public LeaderSkillLevel navalStrategic; // command in a theater / organization / Planning /  generate better combat
        public LeaderSkillLevel navalOperational; // command a fleet / command in a battle
        public LeaderSkillLevel navalTactical; // command a ship / low-level group
        public List<LeaderTrait> traits = new();
        // public float courage; // determine flee / retreat behaviour and buff ship morale
        // public float staff; // how much buff its commander
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }
}