using System;
using System.Collections.Generic;
using System.Linq;
using BulletML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Particles;
using MonoGame.Extended.Particles.Modifiers;
using MonoGame.Extended.Particles.Modifiers.Containers;
using MonoGame.Extended.Particles.Profiles;
using MonoGame.Extended.TextureAtlases;
using Xmas_Hell.BulletML;
using Xmas_Hell.Entities;
using Xmas_Hell.Particles;
using Xmas_Hell.Physics;
using Bullet = Xmas_Hell.Entities.Bullet;

namespace Xmas_Hell
{
    public class GameManager
    {
        private XmasHell _game;
        private List<Bullet> _bullets;

        // BulletML
        public MoverManager MoverManager;
        static public FloatDelegate GameDifficulty;

        // Physics
        public readonly CollisionWorld CollisionWorld;

        // Particles
        public readonly ParticleManager ParticleManager;

        public List<Mover> GetBossBullets()
        {
            return MoverManager.Movers;
        }

        public List<Bullet> GetPlayerBullets()
        {
            return _bullets.Where(b => b is PlayerBullet).ToList();
        }

        public GameManager(XmasHell game)
        {
            _game = game;
            _bullets = new List<Bullet>();
            MoverManager = new MoverManager(_game);
            CollisionWorld = new CollisionWorld(_game);
            ParticleManager = new ParticleManager(_game);
        }

        public void Initialize()
        {
            ParticleManager.Initialize();
        }

        public void Update(GameTime gameTime)
        {
            foreach (var bullet in _bullets)
                bullet.Update(gameTime);

            MoverManager.Update();
            CollisionWorld.Update(gameTime);
            ParticleManager.Update(gameTime);

            _bullets.RemoveAll(b => !b.Used);
        }

        public void AddBullet(Bullet bullet)
        {
            _bullets.Add(bullet);
        }

        public void Draw(GameTime gameTime)
        {
            // TODO: Apply a bloom effect on all bullets
            _game.SpriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.AlphaBlend,
                transformMatrix: _game.ViewportAdapter.GetScaleMatrix()
            );

            foreach (var bullet in _bullets)
                bullet.Draw(gameTime);

            MoverManager.Draw(_game.SpriteBatch);

            _game.SpriteBatch.End();

            _game.SpriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.AlphaBlend,
                transformMatrix: _game.ViewportAdapter.GetScaleMatrix()
            );

            CollisionWorld.Draw(_game.SpriteBatch);
            ParticleManager.Draw(_game.SpriteBatch);

            _game.SpriteBatch.End();
        }
    }
}