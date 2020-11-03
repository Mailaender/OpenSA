﻿#region Copyright & License Information
/*
 * Copyright 2019-2020 The OpenSA Developers (see CREDITS)
 * This file is part of OpenSA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenSA.Warheads
{
	[Desc("Allows to fire a weapon to a directly specified target position relative to the warhead explosion.")]
	public class FireFragmentWarhead : Warhead, IRulesetLoaded<WeaponInfo>
	{
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("Has to be defined in weapons.yaml as well.")]
		public readonly string Weapon = null;

		[Desc("Percentual chance the fragment is fired.")]
		public readonly int Chance = 100;

		[Desc("Target offsets relative to warhead explosion.")]
		public readonly WVec[] Offsets = { new WVec(0, 0, 0) };

		[Desc("If set, Offset's Z value will be used as absolute height instead of explosion height.")]
		public readonly bool UseZOffsetAsAbsoluteHeight = false;

		[Desc("Should the weapons be fired around the intended target or at the explosion's epicenter.")]
		public readonly bool AroundTarget = false;

		[Desc("Rotate the fragment weapon based on the impact orientation.")]
		public readonly bool Rotate = false;

		WeaponInfo weapon;

		public void RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			if (!rules.Weapons.TryGetValue(Weapon.ToLowerInvariant(), out weapon))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(Weapon.ToLowerInvariant()));
		}

		public override void DoImpact(Target target, WarheadArgs args)
		{
			var firedBy = args.SourceActor;
			if (!target.IsValidFor(firedBy))
				return;

			var world = firedBy.World;
			var map = world.Map;

			if (Chance < world.SharedRandom.Next(100))
				return;

			var epicenter = AroundTarget && args.WeaponTarget.Type != TargetType.Invalid
				? args.WeaponTarget.CenterPosition
				: target.CenterPosition;

			foreach (var offset in Offsets)
			{
				WVec targetVector;
				if (UseZOffsetAsAbsoluteHeight)
					targetVector = new WPos(epicenter.X + offset.X, epicenter.Y + offset.Y,
						map.CenterOfCell(map.CellContaining(epicenter)).Z + offset.Z) - epicenter;
				else
					targetVector = offset;

				if (Rotate && args.ImpactOrientation != WRot.None)
					targetVector.Rotate(args.ImpactOrientation);

				var fragmentTarget = Target.FromPos(epicenter + targetVector);
				var fragmentFacing = (fragmentTarget.CenterPosition - target.CenterPosition).Yaw;

				var projectileArgs = new ProjectileArgs
				{
					Weapon = weapon,
					Facing = fragmentFacing,
					CurrentMuzzleFacing = () => fragmentFacing,

					DamageModifiers = args.DamageModifiers,

					InaccuracyModifiers = !firedBy.IsDead ? firedBy.TraitsImplementing<IInaccuracyModifier>()
						.Select(a => a.GetInaccuracyModifier()).ToArray() : new int[0],

					RangeModifiers = !firedBy.IsDead ? firedBy.TraitsImplementing<IRangeModifier>()
						.Select(a => a.GetRangeModifier()).ToArray() : new int[0],

					Source = target.CenterPosition,
					CurrentSource = () => target.CenterPosition,
					SourceActor = firedBy,
					GuidedTarget = fragmentTarget,
					PassiveTarget = fragmentTarget.CenterPosition
				};

				if (projectileArgs.Weapon.Projectile != null)
				{
					var projectile = projectileArgs.Weapon.Projectile.Create(projectileArgs);
					if (projectile != null)
						firedBy.World.AddFrameEndTask(w => w.Add(projectile));

					if (projectileArgs.Weapon.Report != null && projectileArgs.Weapon.Report.Any())
						Game.Sound.Play(SoundType.World, projectileArgs.Weapon.Report.Random(firedBy.World.SharedRandom), target.CenterPosition);
				}
			}
		}
	}
}
