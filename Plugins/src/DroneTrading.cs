using Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("DroneTrading", "TeamDMA", "1.0.0")]
    [Description("Allows trading with drone feature")]
    class DroneTrading : RustPlugin
    {


        #region Oxide Hooks
        private void Init()
        {
            Puts("DroneTrading loaded.");
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner?.GetOwnerPlayer();
            if (player == null)
                return;

            Drone drone = gameObject.ToBaseEntity() as Drone;
            if (drone != null)
            {
                DroneController droneController = gameObject.AddComponent<DroneController>();
                droneController.StartFlying(player, gameObject);
                player.ChatMessage("Drone spawned.");
            }
        }
        #endregion

        #region Controller
        private class DroneController : MonoBehaviour
        {
            public Drone Drone { get; private set; }

            public BasePlayer StartPlayer { get; private set; }

            public BasePlayer TargetPlayer { get; private set; }

            public Vector3? trPos { get; private set; }

            private Transform _tr;

            private Rigidbody _rb;

            private Transform _target;


            private void Awake()
            {
                Drone = GetComponent<Drone>();
                Drone.enabled = false;

                _tr = Drone.transform;
                _rb = Drone.GetComponent<Rigidbody>();

            }

            internal void StartFlying(BasePlayer player, GameObject drone)
            {
                StartPlayer = player;
                _target.position = StartPlayer.GetNetworkPosition();
                _target.position.Set(_tr.position.x, _tr.position.y + 50f, _tr.position.z);

                player.ChatMessage("Deine Pos. ist: " + player.GetNetworkPosition());
            }
            private void FixedUpdate()
            {
                if(_rb.position != _target.position)
                {
                    int speed = 5;

                    _tr.position = _rb.position; //debug?

                    Vector3 direction = (_target.position - _rb.position).normalized;

                    _rb.AddForce(direction/speed, ForceMode.Acceleration);
                    //_rb.MovePosition(((Vector3)_tr.position));
                }
                if(_rb.position == _tr.position)
                {
                    _tr.position.Set(_tr.position.x, _tr.position.y + 3f, _tr.position.z);
                    _rb.MovePosition((Vector3)_tr.position);
                }

            }



        }
        #endregion

        #region Commands
        [ChatCommand("trade")]
        private void GiveDrone(BasePlayer player, string command, string[] args)
        {
            const string DRONE_ITEM = "drone";
            player.GiveItem(ItemManager.CreateByName(DRONE_ITEM), BaseEntity.GiveItemReason.PickedUp);
        }

        #endregion

    }
}