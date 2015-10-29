﻿namespace Ensage.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage.Common.AbilityInfo;

    /// <summary>
    /// </summary>
    public static class AbilityExtensions
    {
        #region Static Fields

        private static readonly Dictionary<string, AbilityData> DataDictionary = new Dictionary<string, AbilityData>();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Checks if given ability can be used
        /// </summary>
        /// <param name="ability"></param>
        /// <returns>returns true in case ability can be used</returns>
        public static bool CanBeCasted(this Ability ability)
        {
            return ability != null && ability.Owner != null && ability.AbilityState == AbilityState.Ready
                   && ability.Level > 0;
            //var hero = ObjectMgr.LocalHero;
            //return ability != null && hero != null && ability.Level > 0 && ability.Cooldown <= 0
            //       && ability.ManaCost <= hero.Mana;
        }

        /// <summary>
        ///     Checks if given ability can be used
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case ability can be used</returns>
        public static bool CanBeCasted(this Ability ability, Unit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }

            var canBeCasted = ability.CanBeCasted();
            if (!target.IsMagicImmune())
            {
                return canBeCasted;
            }

            var data = AbilityDatabase.Find(ability.Name);
            return data == null ? canBeCasted : data.MagicImmunityPierce;
        }

        /// <summary>
        ///     Uses prediction to cast given skillshot ability
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case of successfull cast</returns>
        public static bool CastSkillShot(this Ability ability, Unit target)
        {
            var data = AbilityDatabase.Find(ability.Name);
            var owner = ability.Owner as Unit;
            var delay = Game.Ping / 1000 + (float)owner.GetTurnTime(target);
            var speed = 0f;
            var radius = 0f;
            if (data != null)
            {
                if (data.AdditionalDelay > 0)
                {
                    delay += (float)data.AdditionalDelay;
                }
                if (data.Speed != null)
                {
                    speed = ability.GetAbilityData(data.Speed);
                }
                if (data.Width != null)
                {
                    radius = ability.GetAbilityData(data.Width);
                }
            }
            var xyz = Prediction.SkillShotXYZ(owner, target, delay, speed, radius);
            if (!(owner.Distance2D(xyz) <= (ability.CastRange + radius / 2)))
            {
                return false;
            }
            ability.UseAbility(xyz);
            return true;
        }

        /// <summary>
        ///     Uses given ability in case enemy is not disabled or would be chain stunned.
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case of successfull cast</returns>
        public static bool CastStun(this Ability ability, Unit target)
        {
            if (!ability.CanBeCasted())
            {
                return false;
            }
            var data = AbilityDatabase.Find(ability.Name);
            var owner = ability.Owner;
            var delay = Game.Ping / 1000 + ability.GetCastPoint();
            var radius = 0f;
            if (!ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                delay += (float)owner.GetTurnTime(target);
            }
            if (data != null)
            {
                if (data.AdditionalDelay > 0)
                {
                    delay += (float)data.AdditionalDelay;
                }
                if (data.Speed != null)
                {
                    var speed = ability.GetAbilityData(data.Speed);
                    delay += owner.Distance2D(target) / speed;
                }
                if (data.Radius != 0)
                {
                    radius = data.Radius;
                }
                else if (data.StringRadius != null)
                {
                    radius = ability.GetAbilityData(data.StringRadius);
                }
                else if (data.Width != null)
                {
                    radius = ability.GetAbilityData(data.Width);
                }
            }
            var canUse = Utils.ChainStun(target, delay, null, false);
            if (!canUse)
            {
                return false;
            }
            if (ability.AbilityBehavior.HasFlag(AbilityBehavior.UnitTarget))
            {
                ability.UseAbility(target);
            }
            else if (ability.AbilityBehavior.HasFlag(AbilityBehavior.AreaOfEffect))
            {
                ability.CastSkillShot(target);
            }
            else if (ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                if (target.Distance2D(owner) > radius)
                {
                    return false;
                }
                ability.UseAbility();
            }
            Utils.Sleep(delay * 1000, "CHAINSTUN_SLEEP");
            return true;
        }

        /// <summary>
        ///     Returns ability data with given name, checks if data are level dependent or not
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="dataName"></param>
        /// <param name="level">Custom level</param>
        /// <returns></returns>
        public static float GetAbilityData(this Ability ability, string dataName, uint level = 0)
        {
            var lvl = ability.Level;
            AbilityData data;
            if (!DataDictionary.TryGetValue(ability.Name + "_" + dataName, out data))
            {
                data = ability.AbilityData.FirstOrDefault(x => x.Name == dataName);
                DataDictionary.Add(ability.Name + "_" + dataName, data);
            }
            if (level > 0)
            {
                lvl = level;
            }
            if (data == null)
            {
                return 0;
            }
            return data.Count > 1 ? data.GetValue(lvl - 1) : data.Value;
        }

        /// <summary>
        ///     Returns castpoint of given ability
        /// </summary>
        /// <param name="ability"></param>
        /// <returns></returns>
        public static double GetCastPoint(this Ability ability)
        {
            if (ability is Item)
            {
                return 0;
            }
            try
            {
                var value =
                    Game.FindKeyValues(ability.Name + "/AbilityCastPoint", KeyValueSource.Ability).StringValue;
                if (value.Length > 7)
                {
                    return Convert.ToSingle(value.Split(' ')[ability.Level - 1]);
                }
                return Convert.ToSingle(value);
            }
            catch (Exception)
            {
                return 0;
            }
            return 0;
        }

        /// <summary>
        ///     Returns cast range of ability, if ability is NonTargeted it will return its radius!
        /// </summary>
        /// <param name="ability"></param>
        /// <returns></returns>
        public static float GetCastRange(this Ability ability)
        {
            if (ability.Name == "templar_assassin_meld")
            {
                return (ability.Owner as Hero).GetAttackRange() + 50;
            }
            if (!ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                return ability.CastRange + 50;
            }
            var radius = 0f;
            AbilityInfo data;
            if (!AbilityDamage.dataDictionary.TryGetValue(ability, out data))
            {
                data = AbilityDatabase.Find(ability.Name);
                AbilityDamage.dataDictionary.Add(ability, data);
            }
            if (data.Width != null)
            {
                radius = ability.GetAbilityData(data.Width);
            }
            if (data.StringRadius != null)
            {
                radius = ability.GetAbilityData(data.StringRadius);
            }
            if (data.Radius > 0)
            {
                radius = data.Radius;
            }
            return radius + 50;
        }

        #endregion
    }
}