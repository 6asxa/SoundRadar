import sys
import pyaudio
import numpy as np
import logging
import tkinter as tk
from tkinter import ttk
import threading
import time
import ctypes
import keyboard
import pystray
import json
from pystray import Icon, Menu, MenuItem
from PIL import Image, ImageDraw
import re

# Настройка логирования
logging.basicConfig(filename='soundradar.log', level=logging.DEBUG,
                   format='%(asctime)s - %(levelname)s - %(message)s',
                   filemode='w')  # 'w' mode creates a new file or truncates the existing one

# Добавляем обработчик для вывода логов в консоль
console = logging.StreamHandler()
console.setLevel(logging.INFO)
formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
console.setFormatter(formatter)
logging.getLogger('').addHandler(console)

logging.info("Запуск программы")

class DeviceSelector:
    settings_file = "settings.json"
    def __init__(self, root, on_device_selected):
        self.root = root
        self.root.protocol("WM_DELETE_WINDOW", self.quit_application)
        self.root.title('SoundRadar')
        self.set_window_icon()
        self.root.geometry('851x510')
        self.on_device_selected = on_device_selected
        self.is_minimized = False
        self.root.attributes('-alpha', 1)
        self.canvas = tk.Canvas(self.root, highlightthickness=0, bd=0)
        # Цвета градиента
        self.start_color = (255, 105, 180)  # Розовый
        self.end_color = (138, 43, 226)     # Фиолетовый
        
        # Настройка Canvas для фона
        self.canvas.config(bg='#272727')
        self.canvas.pack(fill=tk.BOTH, expand=True)
        
        # Основной контейнер
        self.content_frame = ttk.Frame(self.canvas)
        self.content_frame.place(relx=0.5, rely=0.5, anchor='center', width=1900)
        
        # Инициализация стилей
        self.style = ttk.Style()
        self._configure_styles()
        self.initUI()
        self.center_window()
        self.load_and_apply_settings()
        
    def load_and_apply_settings(self):
        saved_settings = self.load_settings()
        if saved_settings:
            # Установите сохраненное устройство, если оно есть в списке доступных
            if "device_name" in saved_settings:
                self.device_combo.set(saved_settings["device_name"])
                
            if "channels" in saved_settings:
                self.channel_combo.set(saved_settings["channels"])
         
    def load_settings(self):
        try:
            with open(self.settings_file, "r") as file:
                settings = json.load(file)
            return settings
        except (FileNotFoundError, json.JSONDecodeError) as e:
            logging.error(f"Ошибка загрузки настроек: {e}")
            return {}

    def quit_application(self):
        self.root.destroy()
        sys.exit()  # Завершаем приложение
        
    def save_settings(self, device_name, channels):
        settings = {"device_name": device_name, "channels": channels}
        with open(self.settings_file, "w") as file:
            json.dump(settings, file)
        logging.info("Настройки сохранены")

    def load_settings(self):
        try:
            with open(self.settings_file, "r") as file:
                settings = json.load(file)
            return settings
        except (FileNotFoundError, json.JSONDecodeError) as e:
            logging.error(f"Ошибка загрузки настроек: {e}")
            return {}

    def _configure_styles(self):
        """Настройка стилей виджетов"""
        self.style.configure('TFrame', background='#272727')
        self.style.configure('.TLabelframe', 
                           background='#272727',
                           foreground='black',
                           font=('Arial', 12),
                           relief='flat',
                           borderwidth=0,
)
        self.style.configure('White.TLabel', 
                           background='#272727',
                           foreground='white',
                           font=('Arial', 11))
        self.style.configure('Glass.TButton', 
                           background='#9E9E9E',
                           foreground='#272727',
                           borderwidth=0,
                           font=('Arial', 12))
        self.style.configure('Custom.TCombobox',
                           fieldbackground='',
                           foreground='',
                           selectbackground='')

    def set_window_icon(self):
        """Установка иконки окна"""
        try:
            self.root.iconbitmap('soundradar.ico')
        except Exception as e:
            logging.error(f"Ошибка установки иконки: {e}")
            self._create_default_icon()

    def _create_default_icon(self):
        """Создание временной иконки"""
        img = Image.new('RGB', (64, 64), 'black')
        draw = ImageDraw.Draw(img)
        draw.ellipse((16, 16, 48, 48), fill='red')
        img.save('temp_icon.ico')
        self.root.iconbitmap('temp_icon.ico')

    def initUI(self):
        """Инициализация интерфейса"""
        # Секция выбора устройства
        device_frame = tk.Frame(self.content_frame,
                                bg='#272727',
                                borderwidth=0,
                                highlightthickness=0,
                                )
        device_frame.grid(row=0, column=0, sticky="ew", padx=(1200, 0), pady=10)
        device_frame.columnconfigure(0, weight=1)    
        ttk.Label(device_frame, 
            text="Микрофон:", 
            background='#272727',
            foreground='white').grid(row=0, 
                                     column=0, 
                                     padx=(5, 2), 
                                     sticky='w')
    
        self.device_combo = ttk.Combobox(device_frame,
                                   state="readonly",
                                   font=('Arial', 11),
                                   width=15,
                                   style='Custom.TCombobox')

        self.device_combo.grid(row=1, column=0, padx=(5, 2), pady=5, sticky='w')
    
        # Заполнение устройств
        devices = self.get_audio_devices()
        if devices:
            self.device_combo['values'] = devices
            self.device_combo.current(0)
            # Разблокируем кнопку если есть устройства
            btn_state = tk.NORMAL  
        else:
            self.device_combo['values'] = ["Устройства не найдены"]
            self.device_combo.config(state="disabled")
            btn_state = tk.DISABLED  # Блокируем кнопку если нет устройств
    
        # Секция выбора каналов
        channel_frame = tk.Frame(self.content_frame,
                                bg='#272727',
                                borderwidth=0,
                                highlightthickness=0,
                                )
        channel_frame.grid(row=1, column=0, sticky="ew", padx=(1200, 0), pady=10)
        channel_frame.columnconfigure(0, weight=1)
    
        ttk.Label(channel_frame, text="Каналы:", 
                  background='#272727', 
                  foreground='white').grid(row=0, 
                                           column=0, 
                                           padx=(5, 2), 
                                           sticky='w')
    
        self.channel_combo = ttk.Combobox(channel_frame, values=["2 (Стерео)", 
                                                                 "6 (5.1)", 
                                                                 "8 (7.1)"], 
                                          state="readonly", 
                                          font=('Arial', 11), 
                                          width=15)
        self.channel_combo.grid(row=1, 
                                column=0, 
                                padx=(5, 2), 
                                pady=5, 
                                sticky='w')
    
        # Кнопка применения настроек
        self.apply_btn = tk.Button(self.content_frame,
                             text='Продолжить',  # Изменено название
                             bg= '#9E9E9E',
                             command=self.select_device,)  # Устанавливаем начальное состояние
        self.apply_btn.grid(row=2, 
                            column=0, 
                            padx=(1204,2), 
                            pady=20, 
                            sticky='ew')

    def get_audio_devices(self):
        device_list = []
        try:
            p = pyaudio.PyAudio()
            try:
                for i in range(p.get_device_count()):
                    device_info = p.get_device_info_by_index(i)
                    if (device_info['maxInputChannels'] > 0 and
                        device_info['hostApi'] == 0):  # 0 соответствует Windows DirectSound
                        try:
                            # Пытаемся открыть поток для проверки доступности устройства
                            stream = p.open(format=pyaudio.paInt16,
                                            channels=2,
                                            rate=44100,
                                            input=True,
                                            input_device_index=i,
                                            frames_per_buffer=1024,
                                            start=False)
                            stream.start_stream()
                            stream.stop_stream()
                            stream.close()
                            # Если поток успешно открыт, добавляем устройство в список
                            device_list.append(device_info['name'])
                        except:
                            # Если поток не удалось открыть, пропускаем устройство
                            pass
            finally:
                p.terminate()
        except Exception as e:
            logging.error(f"Ошибка при инициализации PyAudio: {e}")
        return device_list

    def select_device(self):
        selected_device = self.device_combo.get()
        if selected_device and selected_device != "Устройства не найдены":
            channel_text = self.channel_combo.get()
            if channel_text == "2 (Стерео)":
                channels = 2
            elif channel_text == "6 (5.1)":
                channels = 6
            elif channel_text == "8 (7.1)":
                channels = 8
            else:
                channels = 2
        
            # Сохраняем настройки
            self.save_settings(selected_device, channels)
        
            logging.info(f"Выбрано устройство: {selected_device}, каналы: {channels}")
            self.minimize_to_tray()
            self.on_device_selected(selected_device, channels, self)

    def minimize_to_tray(self):
        """Сворачивание в трей"""
        if self.is_minimized:
            self.bring_to_front()
        else:
            self.root.withdraw()
            self.is_minimized = True

    def bring_to_front(self):
        """Восстановление из трея"""
        self.root.deiconify()
        self.root.lift()
        self.root.focus_force()
        self.is_minimized = False

    def center_window(self):
        """Центрирование окна"""
        self.root.update_idletasks()
        width = self.root.winfo_width()
        height = self.root.winfo_height()
        x = (self.root.winfo_screenwidth() // 2) - (width // 2)
        y = (self.root.winfo_screenheight() // 2) - (height // 2)
        self.root.geometry(f'+{x}+{y}')
        

class SoundRadar:
    def __init__(self, device_name, channels, device_selector):
        self.root = tk.Toplevel()
        self.device_selector = device_selector  # Сохраните ссылку

        # Устанавливаем размеры окна
        self.window_width = 125
        self.window_height = 125
        self.root.geometry(f"{self.window_width}x{self.window_height}")
        self.root.resizable(False, False)
        self.tray = SoundRadarTray(self, self.device_selector)
        threading.Thread(target=self.tray.run, daemon=True).start()

        # Делаем окно поверх всех окон и без рамки
        self.root.attributes('-topmost', True)
        self.root.overrideredirect(True)
        self.is_minimized = False

        # Устанавливаем полупрозрачный черный фон
        self.root.configure(bg='black')
        self.root.attributes('-alpha', 0.6)  # Уровень прозрачности (0.0-1.0)

        # Центрируем окно и смещаем ниже от верхнего края
        screen_width = self.root.winfo_screenwidth()
        screen_height = self.root.winfo_screenheight()
        x = (screen_width - self.window_width) // 2
        y = (screen_height - self.window_height) // 2 + 300  # Смещаем на 300 пикселей вниз
        self.root.geometry(f"+{x}+{y}")

        self.device_name = device_name
        self.channel_count = channels
        self.background_noise = 0
        self.alpha = 0.1
        self.square_size = 15
        self.square_pos = (self.window_width//2 - self.square_size//2, 
                          self.window_height//2 - self.square_size//2)

        self.channel_positions = {
            2: ["Left", "Right"],  # Стерео
            6: ["Front Left", "Front Right", "Front Center", "Low Frequency", "Back Left", "Back Right"],  # 5.1
            8: ["Front Left", "Front Right", "Front Center", "Low Frequency", "Back Left", "Back Right", "Side Left", "Side Right"]  # 7.1
        }

        self.stream = None
        self.running = True

        self.initUI()
        self.initAudio()

        # Запускаем поток для обработки звука
        self.audio_thread = threading.Thread(target=self.process_audio)
        self.audio_thread.daemon = True
        self.audio_thread.start()

        # Добавляем обработчик закрытия окна
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

        # Добавляем горячую клавишу для закрытия (Esc)
        self.root.bind('<Escape>', lambda e: self.on_close())

         # Регистрируем глобальную горячую клавишу для возвращения окна поверх всех окон
        keyboard.add_hotkey('ctrl+alt+s', self.bring_to_front)
        logging.info("Зарегистрирована горячая клавиша Ctrl+Alt+S для возвращения окна поверх всех окон")
        
    def toggle_tray(self):
        """Переключает состояние окна: сворнуть/восстановить"""
        if self.root.state() == 'withdrawn':
            self.root.deiconify()
            self.bring_to_front()
            self.is_minimized = False
        else:
            self.root.withdraw()
            self.is_minimized = True
        

    def initUI(self):
        self.canvas = tk.Canvas(self.root, bg='black', highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True)
        self.square_id = self.canvas.create_rectangle(
            *self.square_pos, 
            self.square_pos[0] + self.square_size, 
            self.square_pos[1] + self.square_size,  
            fill='red'
        )
        # Делаем окно прозрачным для мыши (события мыши будут проходить сквозь окно)
        self.root.update_idletasks()  # Обновляем окно, чтобы получить его hwnd
        hwnd = ctypes.windll.user32.GetParent(self.root.winfo_id())
        GWL_EXSTYLE = -20
        WS_EX_LAYERED = 0x00080000
        WS_EX_TRANSPARENT = 0x00000020
        style = ctypes.windll.user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        style = style | WS_EX_LAYERED | WS_EX_TRANSPARENT
        ctypes.windll.user32.SetWindowLongW(hwnd, GWL_EXSTYLE, style)
    
        # Устанавливаем прозрачность окна
        LWA_ALPHA = 0x00000002
        ctypes.windll.user32.SetLayeredWindowAttributes(hwnd, 0, int(0.6*255), LWA_ALPHA)
        # Добавляем обработчики событий мыши, чтобы игнорировать их
        self.root.bind("<Button-1>", self.ignore_mouse_event)  # Игнорировать клик левой кнопкой
        self.root.bind("<Button-2>", self.ignore_mouse_event)  # Игнорировать клик средней кнопкой
        self.root.bind("<Button-3>", self.ignore_mouse_event)  # Игнорировать клик правой кнопкой
        self.root.bind("<ButtonRelease-1>", self.ignore_mouse_event)  # Игнорировать отпускание кнопки
        self.root.bind("<ButtonRelease-2>", self.ignore_mouse_event)
        self.root.bind("<ButtonRelease-3>", self.ignore_mouse_event)
        self.root.bind("<Motion>", self.ignore_mouse_event)  # Игнорировать движение мыши
        self.root.bind("<B1-Motion>", self.ignore_mouse_event)  # Игнорировать перетаскивание
        self.root.bind("<B2-Motion>", self.ignore_mouse_event)
        self.root.bind("<B3-Motion>", self.ignore_mouse_event)
        self.root.bind("<Double-Button-1>", self.ignore_mouse_event)  # Игнорировать двойной клик
        self.root.bind("<Double-Button-2>", self.ignore_mouse_event)
        self.root.bind("<Double-Button-3>", self.ignore_mouse_event)
        self.root.bind("<Enter>", self.ignore_mouse_event)  # Игнорировать вход курсора в окно
        self.root.bind("<Leave>", self.ignore_mouse_event)  # Игнорировать выход курсора из окна
        self.root.bind("<MouseWheel>", self.ignore_mouse_event)  # Игнорировать колесо мыши

    def initAudio(self):
        try:
            self.p = pyaudio.PyAudio()
            device_index = self.get_device_index(self.device_name)
            if device_index is None:
                logging.error(f"Устройство {self.device_name} не найдено")
                return

            # Получаем информацию об устройстве
            device_info = self.p.get_device_info_by_index(device_index)
            max_channels = int(device_info['maxInputChannels'])

            # Проверяем, поддерживает ли устройство выбранное количество каналов
            if self.channel_count > max_channels:
                logging.warning(f"Устройство поддерживает только {max_channels} каналов. Используется {max_channels}.")
                self.channel_count = max_channels

            # Открываем аудиопоток
            self.stream = self.p.open(format=pyaudio.paInt16,
                                     channels=self.channel_count,
                                     rate=44100,
                                     input=True,
                                     frames_per_buffer=1024,
                                     input_device_index=device_index)
            logging.info(f"Аудиопоток инициализирован с {self.channel_count} каналами")
        except Exception as e:
            logging.error(f"Ошибка при инициализации аудиопотока: {e}")
            self.stream = None

    def get_device_index(self, device_name):
        """Возвращает индекс устройства по его имени."""
        p = pyaudio.PyAudio()
        for i in range(p.get_device_count()):
            device_info = p.get_device_info_by_index(i)
            if device_info["name"] == device_name:
                return i
        return None

    def process_audio(self):
        while self.running:
            try:
                if self.stream is None:
                    logging.warning("Аудиопоток не инициализирован")
                    time.sleep(0.1)
                    continue

                data = np.frombuffer(self.stream.read(1024, exception_on_overflow=False), dtype=np.int16)

                # Разделяем данные по каналам
                channels = [data[i::self.channel_count] for i in range(self.channel_count)]

                # Вычисляем громкость для каждого канала
                volumes = [np.sqrt(np.mean(np.square(channel.astype(np.float32)))) for channel in channels]

                # Обновляем фоновый шум
                if any(volumes):
                    self.background_noise = self.alpha * np.mean(volumes) + (1 - self.alpha) * self.background_noise

                # Устанавливаем динамический порог
                threshold = self.background_noise * 1.5

                # Если громкость выше порога, обрабатываем звук
                if any(volume > threshold for volume in volumes):
                    # Находим канал с максимальной громкостью
                    max_volume_index = np.argmax(volumes)
                    max_volume = volumes[max_volume_index]

                    # Получаем позицию канала
                    channel_position = self.channel_positions[self.channel_count][max_volume_index]

                    # Вычисляем новую позицию квадрата в зависимости от канала
                    center_x = self.window_width // 2 - self.square_size // 2
                    center_y = self.window_height // 2 - self.square_size // 2

                    if self.channel_count == 2:  # Стерео
                        normalized_diff = (volumes[0] - volumes[1]) / max_volume
                        new_x = center_x - int(normalized_diff * (self.window_width - self.square_size) // 2)
                        new_y = center_y
                    else:  # 5.1 или 7.1
                        if "Front" in channel_position:
                            new_y = center_y - 20  # Смещаем вверх
                        elif "Back" in channel_position:
                            new_y = center_y + 20  # Смещаем вниз
                        else:
                            new_y = center_y

                        if "Left" in channel_position:
                            new_x = center_x - 20  # Смещаем влево
                        elif "Right" in channel_position:
                            new_x = center_x + 20  # Смещаем вправо
                        else:
                            new_x = center_x

                    # Ограничиваем позицию в пределах окна
                    new_x = max(0, min(self.window_width - self.square_size, new_x))
                    new_y = max(0, min(self.window_height - self.square_size, new_y))

                    # Обновляем позицию квадрата через главный поток
                    self.root.after(0, self.update_square_position, new_x, new_y)
                else:
                    # Если звука нет, возвращаем квадрат в центр
                    center_x = self.window_width // 2 - self.square_size // 2
                    center_y = self.window_height // 2 - self.square_size // 2
                    self.root.after(0, self.update_square_position, center_x, center_y)

            except Exception as e:
                logging.error(f"Ошибка при обработке звука: {e}")
                time.sleep(0.1)

    def update_square_position(self, x, y):
        self.square_pos = (x, y)
        self.canvas.coords(
            self.square_id, 
            x, y, 
            x + self.square_size, 
            y + self.square_size
        )
        
    def ignore_mouse_event(self, event):
        """Метод для игнорирования всех событий мыши"""
        return "break"  # Предотвращает дальнейшую обработку события

    def bring_to_front(self):
        """Метод для возвращения окна поверх всех окон по горячей клавише"""
        try:
            self.root.attributes("-topmost", False)
            self.root.attributes("-topmost", True)
            self.root.lift()

            screen_width = self.root.winfo_screenwidth()
            screen_height = self.root.winfo_screenheight()
            x = (screen_width - self.window_width) // 2
            y = (screen_height - self.window_height) // 2 + 300  # Смещаем на 300 пикселей вниз
            self.root.geometry(f"+{x}+{y}")

            logging.info("Окно возвращено поверх всех окон")
        except Exception as e:
            logging.error(f"Ошибка при возвращении окна поверх всех окон: {e}")
            
    # В классе SoundRadar измените метод minimize_to_tray:
    def minimize_to_tray(self, hide_window=True):
        if hide_window:
            self.root.withdraw()
        
        # Создаем трей только если он не существует или неактивен
        if not hasattr(self, 'tray') or not self.tray.is_running:
            self.tray = SoundRadarTray(self, self.device_selector)
            if self.tray.icon:  # Проверка успешности создания
                threading.Thread(target=self.tray.run, daemon=True).start()
            else:
                logging.error("Не удалось создать трей!")


    def on_close(self):
        self.running = False
        # Отменяем регистрацию горячей клавиши
        try:
            keyboard.remove_hotkey('ctrl+alt+s')
        except:
            pass
        if self.audio_thread.is_alive():
            self.audio_thread.join(timeout=1)
        # Останавливаем иконку в трее, если она существует
        if hasattr(self, 'tray') and hasattr(self.tray, 'icon'):
            self.tray.stop()
        if self.stream is not None:
            self.stream.stop_stream()
            self.stream.close()
        if hasattr(self, 'p'):
            self.p.terminate()
        logging.info("Приложение закрыто")
        self.root.destroy()

class SoundRadarTray:
    _active_icon = None  # Классовая переменная для отслеживания активной иконки
    _icon_image = None

    def __init__(self, radar_instance, device_selector):
        # Останавливаем предыдущую иконку, если она существует
        if SoundRadarTray._active_icon and SoundRadarTray._active_icon.is_running:
            SoundRadarTray._active_icon.stop()
            
        self.radar_instance = radar_instance
        self.device_selector = device_selector
        self._running = False
        self.icon = None
        self._init_icon()
        SoundRadarTray._active_icon = self
        
    def _init_icon(self):
        """Инициализация иконки с защитой от ошибок"""
        # Загрузка изображения
        if SoundRadarTray._icon_image is None:
            try:
                SoundRadarTray._icon_image = Image.open("soundradar.ico")
            except:
                SoundRadarTray._icon_image = self._create_default_icon()
        
        # Создание меню
        try:
            menu = Menu(
                MenuItem('Показать', self.show_app),
                MenuItem('Настройки', self.show_settings),
                MenuItem('Выход', self.exit_app)
            )
        except Exception as e:
            logging.error(f"Ошибка меню: {e}")
            menu = Menu()
        
        # Создание иконки
        try:
            self.icon = Icon(
                'soundradar',
                SoundRadarTray._icon_image,
                "Sound Radar",
                menu
            )
        except Exception as e:
            logging.error(f"Ошибка иконки: {e}")
            self.icon = None
            
    def stop(self):
        """Остановка иконки трея и обновление состояния"""
        if self._running:
            if self.icon:
                self.icon.stop()  # Останавливаем иконку
            self._running = False  # Обновляем флаг состояния
            logging.info("Иконка трея остановлена")    
        
    def show_app(self, icon=None):
        """Показывает главное окно приложения"""
        try:
            self.radar_instance.root.deiconify()
            self.radar_instance.bring_to_front()
            self.radar_instance.is_minimized = False
            logging.info("Окно приложения показано из трея")
        except Exception as e:
            logging.error(f"Ошибка при показе окна: {e}")
            
    def show_settings(self, icon=None):
        """Показывает окно настроек"""
        try:
            self.device_selector.bring_to_front()
        except Exception as e:
            logging.error(f"Ошибка при открытии настроек: {e}")

    def _create_default_icon(self):
        """Создание иконки по умолчанию"""
        image = Image.new('RGB', (64, 64), color='black')
        draw = ImageDraw.Draw(image)
        draw.ellipse((10, 10, 54, 54), fill='red')
        return image
        
    @property
    def is_running(self):
        """Проверка активности иконки"""
        return self._running and self.icon is not None

    def exit_app(self):
        """Корректное завершение приложения"""
        # Останавливаем Radar
        if self.radar_instance:
            self.radar_instance.on_close()
        
        # Уничтожаем окно настроек
        if self.device_selector and self.device_selector.root.winfo_exists():
            self.device_selector.root.destroy()
        
        # Останавливаем иконку
        if self.icon:
            self.icon.stop()
        
        # Корректный выход из приложения
        sys.exit(0)
        
    def run(self):
        """Безопасный запуск иконки"""
        if self.icon:
            self._running = True
            self.icon.run()
        else:
            logging.error("Иконка трея не была инициализирована")
            self._running = False
        
def on_device_selected(device_name, channels, device_selector):
    radar = SoundRadar(device_name, channels, device_selector)
    
    # Регистрируем горячую клавишу для переключения окна
    keyboard.add_hotkey('ctrl+alt+t', lambda: radar.toggle_tray())
    
    radar.root.mainloop()

if __name__ == '__main__':
    root = tk.Tk()
    
    def callback_wrapper(name, ch, ds):
        on_device_selected(name, ch, ds)
         
    device_selector = DeviceSelector(root, callback_wrapper)
    root.mainloop()