using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BuildingBlocker", "TeamDMA", "0.0.1")]
    [Description("Prevents the placement of ladders/twigs where building is blocked")]

    class BuildingBlocker : RustPlugin
    {

        List<uint> BlockedConstructionWhilePVE = new List<uint>()
        {
            2150203378,
            72949757,
            2194854973,
            916411076,
            3234260181,
            2925153068,
            1886694238,
            372561515,
            995542180,
            870964632,
            3895720527
        };


        private void Unload()
        {
            AllowBuildingBypass(true);
        }
        private void Init()
        {
            AllowBuildingBypass(false);
        }
        /*object OnConstructionPlace(BaseEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {
            Puts("Object placed: " + component.prefabID + " by " + player.displayName);
            return null;
        }*/
        private void AllowBuildingBypass(bool allow)
        {
            if(BlockedConstructionWhilePVE.Count > 0)
            {
                foreach (uint conID in BlockedConstructionWhilePVE)
                {
                    Construction con = PrefabAttribute.server.Find<Construction>(conID);
                    if (con) con.canBypassBuildingPermission = allow;
                }
            }
        }
    }
}
