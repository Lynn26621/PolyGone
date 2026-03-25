using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace PolyGone.Core
{
    public class AudioManager
    {
        // Setup for content manager to load audio assets
        private ContentManager content;

        // Setup for sound effects
        private SoundEffect jumpSfx;
        private SoundEffect shootSfx;
        private SoundEffect collisionSfx;
        private SoundEffect deathSfx;

        // Setup for songs
        private Song menuSong;
        private Song level1Song;
        private Song level2Song;
        private Song level3Song;
        private Song gameOverSong;
        private Song goalAchievedSong;

        // Variable to track currently playing song
        private string currentSong;

        // Constructor, gives access to content manager and calls method to load audio assets
        public AudioManager(ContentManager content)
        {
            this.content = content;
            LoadAudio();
        }

        // Method to play audio, plays different type/audio asset based on parameters
        public void PlayAudio(string sfx, bool playEffect, string song, bool playSong)
        {
            if (sfx != null && playEffect)
            {
                PlaySfx(sfx);
            }
            if (song != null && playSong)
            {
                //Checks if song is already playing to avoid restarting it
                if (song != currentSong)
                {
                    PlaySong(song);
                }
            }
        }

        // Loads audio assets
        public void LoadAudio()
        {
            jumpSfx = content.Load<SoundEffect>("Audio/jumpSfx");
            shootSfx = content.Load<SoundEffect>("Audio/shootSfx");
            collisionSfx = content.Load<SoundEffect>("Audio/collisionSfx");
            deathSfx = content.Load<SoundEffect>("Audio/deathSfx");
            menuSong = content.Load<Song>("Audio/menuSong");
            level1Song = content.Load<Song>("Audio/level1Song");
            level2Song = content.Load<Song>("Audio/level2Song");
            level3Song = content.Load<Song>("Audio/level3Song");
            gameOverSong = content.Load<Song>("Audio/gameOverSong");
            goalAchievedSong = content.Load<Song>("Audio/goalAchievedSong");
        }

        //Method to play songs
        public void PlaySong(string song)
        {
            // Stops previous song before playing new one
            if(MediaPlayer.State == MediaState.Playing)
            {
                MediaPlayer.Stop();
            }

            // Stores current song
            currentSong = song;
            switch (song)
            {
                case "menuSong":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(menuSong);
                    break;
                case "level1Song":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(level1Song);
                    break;
                case "level2Song":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(level2Song);
                    break;
                case "level3Song":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(level3Song);
                    break;
                case "gameOverSong":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(gameOverSong);
                    break;
                case "goalAchievedSong":
                    MediaPlayer.IsRepeating = true;
                    MediaPlayer.Play(goalAchievedSong);
                    break;
                default:
                    break;
            }
        }

        // Method to play sound effects
        public void PlaySfx(string sfx)
        {
            switch (sfx)
            {
                case "jumpSfx":
                    jumpSfx.Play();
                    break;
                case "shootSfx":
                    shootSfx.Play();
                    break;
                case "collisionSfx":
                    collisionSfx.Play();
                    break;
                case "deathSfx":
                    deathSfx.Play();
                    break;
                default:
                    break;
            }
        }
    }
}
