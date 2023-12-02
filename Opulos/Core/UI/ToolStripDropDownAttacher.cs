using Opulos.Core.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Opulos.Core.UI {

///<summary>
///Attaches a drop down menu to a control, like the drop down window of a combo box. The drop down menu
///moves with the control, and is displayed when the control receives the focus.
///</summary>
public class ToolStripDropDownAttacher : IMessageFilter {

	///<summary>Occurs just before the menu visibility is changed to true, with an option to cancel the show.</summary>
	public event CancelEventHandler MenuShowing;

	///<summary>The menu passed to the constructor.</summary>
	public ToolStripDropDown Menu { get; private set; }

	///<summary>The control passed to the constructor.</summary>
	public Control Control { get; private set; }

	///<summary>Set to a non-null value to auto-close the menu if the focus is lost to another control in the same top level window.</summary>
	public ToolStripDropDownCloseReason? AutoCloseReasonControlFocusLost { get; set; }

	///<summary>Set to a non-null value to auto-close the menu if a different top level window is activated.</summary>
	public ToolStripDropDownCloseReason? AutoCloseReasonWindowLostFocus { get; set; }

	///<summary>An option to re-show the menu when the top level window is restored from a minimized state.</summary>
	public bool ShowMenuOnRestore { get; set; }

	///<summary>An option to re-show the menu when the top level window becomes the foreground window, and the control has the focus.</summary>
	public bool ShowMenuOnActivate { get; set; }

	///<summary>The minimum font size allowed for the drop down menu. The default value is 6. The menu automatically changes font size when the control's font size is changed.</summary>
	public float MinFontSize { get; set; }

	///<summary>When the control's font size is changed, the menu's font size is the control's font size plus the delta value.
	///If all the items in the menu use the same font size as the menu's font size, then a delta value zero will probably look
	///okay. However, if the menu has items that scale up with its base font size, then using a negative delta might look better. 
	///The default value is 0.</summary>
	public float FontSizeDelta { get; set; }

	///<summary>A list of keys that when pressed will open the menu when the control has the focus. The default list only contains Keys.Down.</summary>
	public List<Keys> OpenMenuKeys { get; private set; }

	///<summary>An option to have a mouse click on the control open the menu. Some controls may not want to open the menu on a click. The default value is true.</summary>
	public bool ClickOpensMenu { get; set; }

	///<summary>Set to a non-null value to close the menu if it is currently visible and the control is clicked. ClickOpensMenu must be true for this to work. The default value is CloseCalled.</summary>
	public ToolStripDropDownCloseReason? ClickCloseReason { get; set; }

	///<summary>An option to have the menu open when the control receives the focus. The default value is true.</summary>
	public bool GotFocusOpensMenu { get; set; }

	///<summary>If non-null, this is the value passed to the CloseMenu(...) method when the escape key is pressed. If null then Escape does not close the menu.
	///The default value is ToolStripDropDownCloseReason.Keyboard.</summary>
	public ToolStripDropDownCloseReason? EscapeCloseReason { get; set; }

	///<summary>Defines if the menu aligns to the left of the control or to the right of the control. The default is left alignment.</summary>
	public HorizontalAlignment Alignment { get; set; }

	public bool MirrorControlDropDown { get; set; }

	//private const int WINEVENT_OUTOFCONTEXT = 0;
	//private const int EVENT_OBJECT_CREATE = 0x8000;
	//private const int EVENT_SYSTEM_FOREGROUND = 0x0003;

	//private IntPtr hHook = IntPtr.Zero;
	//private WinEventProc CallWinEventProc;
	//private delegate void WinEventProc(IntPtr hWinEventHook, int iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime);
	private NW nwMenu = null;

	// needed in order for the down arrow to open the menu, otherwise the default behavior is
	// the focus is transferred to the next control when the down arrow is pressed.
	//private bool downKeyPressed = false;
	private CloseReason lastClosedReason;
	private bool wasMinimized = false;
	private bool isFocusing = false;
	private bool isClosing = false;
	private bool downKeyPressed = false;
	private bool isAdjusting = false;
	private NW2 nw2 = new NW2();
	private NW3 nw3 = null;

	///<summary>Attaches events to both the menu and control to simulate a drop down menu that belongs to the control.</summary>
	///<param name="menu">A drop down menu that appears underneath the control, like a combobox.</param>
	///<param name="control">The control that is the owner of the menu.</param>
	///<param name="keepMenuOpen">An open to keep the drop down menu open if the host window is no longer the active window.</param>
	public ToolStripDropDownAttacher(ToolStripDropDown menu, Control control, bool keepMenuOpen = true) {
		Menu = menu;
		Control = control;
		AutoCloseReasonControlFocusLost = ToolStripDropDownCloseReason.AppFocusChange;
		AutoCloseReasonWindowLostFocus = (keepMenuOpen ? (ToolStripDropDownCloseReason?) null : ToolStripDropDownCloseReason.AppFocusChange);
		EscapeCloseReason = ToolStripDropDownCloseReason.Keyboard;
		ClickCloseReason = ToolStripDropDownCloseReason.CloseCalled;
		ClickOpensMenu = true;
		GotFocusOpensMenu = true;
		menu.AutoClose = false;
		MinFontSize = 6;
		OpenMenuKeys = new List<Keys>();
		OpenMenuKeys.Add(Keys.Down);
		OpenMenuKeys.Add(Keys.Enter); // added 2019-12-08 (possibly only do this if control is a Label?)
		OpenMenuKeys.Add(Keys.Space); // added 2019-12-08

		Font oldFont = control.Font;
		Font font = null;

		//System.Threading.Thread t = new System.Threading.Thread(() => {
		//	while (true) {
		//		System.Threading.Thread.Sleep(1000);
		//		control.BeginInvoke((Action) delegate {
		//			System.Diagnostics.Debug.WriteLine("menu.Focused:" + menu.Focused + " control.Focused:" + control.Focused);
		//		});
		//	}
		//});
		//t.IsBackground = true;
		//t.Start();

		//--------------------------------------------------------------------------------
		// Menu events:
		//bool preventClose = false;

		menu.Closing += (o, e) => {
//			if (menu.AutoClose && control.Focused) {
//System.Diagnostics.Debug.WriteLine("prevent close");				
//				preventClose = false;
//				e.Cancel = true;
//				return;
//			}

			// for some reason e.Cancel is set to true
			if (isClosing || e.CloseReason == ToolStripDropDownCloseReason.ItemClicked || e.CloseReason == ToolStripDropDownCloseReason.Keyboard)
				e.Cancel = false;
		};

		//menu.KeyDown += (o, e) => {
		menu.PreviewKeyDown += (o, e) => {
			if (e.KeyCode == Keys.Escape) {
				if (EscapeCloseReason.HasValue && !menu.AutoClose) {
					CloseMenu(EscapeCloseReason.Value);
					//e.Handled = true;
					isClosing = true; // prevent the menu from opening when control gains the focus
					control.Focus();
					isClosing = false;
				}
				else if (menu.AutoClose) {
					// otherwise the first control in the parent control receives the focus
					control.Focus();
				}
			}
		};

		menu.Click += delegate {
			isFocusing = true;
			menu.Focus();
			IntPtr h = GetTopWindow(control.Handle);
			// after the main window is minimized and restored, clicking on
			// clock will deactivate the main window title bar unless this is called:
			SendMessage(h, WM_NCACTIVATE, (IntPtr) 1, (IntPtr) 0);
			isFocusing = false;
		};

		menu.GotFocus += delegate {
			// click on the control, then tab to focus on the drop down
			// then minimize the main window, then restore the main window
			// tabbing into the drop down deactivates the main window
			// Note: when the owner window is minimized, this causes the
			// a lost focus followed by a got focus. However, the top level
			// parent window is about to also be minimized, so in that case
			// do not call WM_NCACTIVATE.
			bool activate = true;
			IntPtr h1 = GetTopWindow(control.Handle);
			IntPtr h3 = GetWindow(h1, GW_OWNER);
			if (h3 != IntPtr.Zero) {
				var wp = GetPlacement(h3);
				if (wp.showCmd == ShowWindowCommands.Hide ||wp.showCmd == ShowWindowCommands.Minimized)
					activate = false;
			}

			if (activate) {
				SendMessage(h1, WM_NCACTIVATE, (IntPtr) 1, (IntPtr) 0);	
				try {
					// this is done otherwise both windows (child and owner) have the title bar appear as activated
					// must be done on BeginInvoke or it doesn't work. Presumably the owner window receives a WM_NCACTIVATE
					// message after the Menu.GotFocus event.
					Menu.BeginInvoke((Action) delegate {
						try {
							SendMessage(h3, WM_NCACTIVATE, (IntPtr) 0, (IntPtr) 0);
						} catch {}
					});
				} catch {}
			}
		};

		menu.LostFocus += delegate {
			// for some strange reason, a LostFocus event occurs when the menu is clicked
			if (isFocusing)
				return;

			if (isClosing)
				return;

			// the control still has the focus, so keep the menu open.
			if (control.Focused)
				return;

			// if another window is clicked, then two issues occur:
			// 1) The top level window doesn't deactivate, so two windows on the
			// desktop both appear active.
			// 2) When the focus returns to this program's main window, clicking
			// on the drop down shows this window's caption as deactivated.

			if (wasMinimized) {
//Debug.WriteLine("Skipping lost focus");
				wasMinimized = false;
				return;
			}

			if (!AutoCloseReasonControlFocusLost.HasValue && !AutoCloseReasonWindowLostFocus.HasValue)
				return;

			// is this window still the top window?
			IntPtr fg = GetForegroundWindow();
			if (fg == menu.Handle) {
				// when the down arrow is pressed, the menu is the foreground window
				// so no reason to deactivate the top window
				return;
			}

			IntPtr h1 = GetTopWindow(control.Handle);
			bool isWindowActive = (h1 == fg);

			if (isWindowActive) {
				if (AutoCloseReasonControlFocusLost.HasValue)
					CloseMenu(AutoCloseReasonControlFocusLost.Value);
			}
			else {
System.Diagnostics.Debug.WriteLine("Menu lost focus and window is not top window.");
				if (AutoCloseReasonWindowLostFocus.HasValue)
					CloseMenu(AutoCloseReasonWindowLostFocus.Value);
			}


			/*
			if (AutoCloseMenuReasonFocusLost.HasValue ||  || downKeyPressed) {
				downKeyPressed = false;
				IntPtr fg = GetForegroundWindow();
				if (fg == menu.Handle) {
					// when the down arrow is pressed, the menu is the foreground window
					// so no reason to deactivate the top window
					return;
				}

				IntPtr h1 = GetTopWindow(c.Handle);
				bool b = (h1 == fg);
				if (!b) {
Debug.WriteLine(h1 + "  " + fg);
					// otherwise both h1 and fg appear as active windows
					SendMessage(h1, WM_NCACTIVATE, (IntPtr) 0, (IntPtr) 0);
					return;
				}
				if (KeepMenuOpen)
					return;
			}
Debug.WriteLine("focus lost, closing menu");
			CloseMenu(ToolStripDropDownCloseReason.CloseCalled);*/
		};

		//CallWinEventProc = new WinEventProc(EventCallback);
		//hHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, CallWinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);		

		if (menu.IsHandleCreated) {
			nwMenu = new NW(this);
			Application.AddMessageFilter(this);
		}
		else {
			menu.HandleCreated += delegate {
				nwMenu = new NW(this);
				Application.AddMessageFilter(this);
			};
		}

		menu.Disposed += delegate {
			if (font != null)
				font.Dispose();

			if (menu != null)
				menu.Dispose();

			Application.RemoveMessageFilter(this);
			font = null;
			menu = null;
		};


		//--------------------------------------------------------------------------------
		// Control events:
		if (control.IsHandleCreated)
			nw2.AssignHandle(control.Handle);
		else {
			control.HandleCreated += delegate {
				nw2.AssignHandle(control.Handle);
			};
		}
		control.Disposed += delegate {
			nw2.ReleaseHandle();
			if (nw3 != null)
				nw3.ReleaseHandle();
		};

		if (control is ComboBox) {
			var combo = (ComboBox) control;
			nw3 = new NW3();

			if (combo.IsHandleCreated) {
				nw3.AssignHandle(combo.Handle);
				//COMBOBOXINFO info = COMBOBOXINFO.GetInfo(combo);
				//nw3.AssignHandle(info.hwndList);
			}
			else {
				combo.HandleCreated += delegate {
					//COMBOBOXINFO info = COMBOBOXINFO.GetInfo(combo);
					//nw3.AssignHandle(info.hwndList);
					nw3.AssignHandle(combo.Handle);
				};
			}

			combo.DropDown += delegate {
				if (MirrorControlDropDown) {
					Debug.WriteLine("combo.DropDownOpened");

					if (!Menu.Visible) {
						combo.BeginInvokeSafe((Action) delegate {
							Debug.WriteLine("combo.DropDownOpened: BeginInvoke > ShowMenu(), preventClose=true, Menu.Focus(), SelectFirstItem(), preventClose=false");

							// use BeginInvoke otherwise the dropdown appears on top of the menu
							ShowMenu();
							// transfer the focus to the Menu
							nw3.preventClose = true;
							Menu.Focus();
							Debug.WriteLine("combo.DropDownOpened: SelectFirstItem() begin");
							SelectFirstItem();
							Debug.WriteLine("combo.DropDownOpened: SelectFirstItem() end");
							//nw3.preventClose = false;				
						});
					}
					else {
						Debug.WriteLine("combo.DropDownOpened: Menu.Visible was already true");
					}
				}
			};

			combo.DropDownClosed += delegate {
				if (MirrorControlDropDown) {
					Debug.WriteLine("combo.DropDownClosed");

					if (Menu.Visible) {
						// in order to allow the user to tab into the DropDown, a close event has to be skipped
						// which puts the Visible state and DropDown state out of sync
						if (!downKeyPressed) {
							CloseMenu(ToolStripDropDownCloseReason.CloseCalled);
						}
					}
					else {
						
					}
				}	
			};
		}

		bool ignore = false;
		menu.VisibleChanged += delegate {
			if (!menu.Visible && menu.AutoClose) {
				// prevents the situation where the menu is visible, the control is clicked,
				// which causes the menu to close, but then open again.
				ignore = true;
				// not sure if there is a better way to do this. If the control is clicked,
				// the menu opens. If the user clicks on a blank area, the menu closes, but
				// the control keeps the focus. If the user clicks the control again, then
				// the menu needs to open. The control always kept the focus, it was just
				// the duration between the next time the control was clicked.
				Control.BeginInvoke((Action) delegate {
					ignore = false;
				});
			}
		};

		//bool isMouseDown = false;
		bool wasJustOpened = false;
		MouseEventHandler mouseDown = new MouseEventHandler((o, e) => {
			if (ignore)
				return;

			wasJustOpened = false;
			//isMouseDown = true;
			// toggle the menu visibility
			if (ClickOpensMenu && !Menu.Visible && (!CanMirror || !MirrorControlDropDown)) {
				wasJustOpened = true;
				ShowMenu();
			}
		});

		MouseEventHandler mouseUp = new MouseEventHandler((o, e) => {
			// toggle the menu visibility
			//bool none = (Control.MouseButtons == MouseButtons.None);
			//!isMouseDown
			if (!wasJustOpened && ClickOpensMenu && Menu.Visible && ClickCloseReason.HasValue && (!CanMirror || !MirrorControlDropDown))
				CloseMenu(ClickCloseReason.Value);
		});
		control.MouseDown += (o, e) => {
			//isMouseDown = true;
			mouseDown(o, e);
		};
		control.MouseUp += (o, e) => {
			mouseUp(o, e);
			//isMouseDown = false;
		};
		nw2.MouseUp += delegate {
			mouseUp(null, null);
			//isMouseDown = false;
		};


		// The LostFocus event happens when another window is selected. However, control 'c' is
		// still the active control on the window. When the window with control 'c' is activated
		// then GotFocus fires and ShowMenu() is called. Is there a better way to detect
		// preventing the window from showing?
		bool reallyLostFocus = true; // added 2016-01-25
		//control.mouse
		control.GotFocus += delegate {
			// the issue is that when the user clicks the control, first the control gets the focus,
			// which opens the Menu, but then the MouseDown event is fired, which closes the menu
			// So a NativeWindow (nw2) is used to detect that a WM_MOUSEACTIVATE was received and
			// the control is about to be activated by mouse, so don't show the menu here.
			bool mouseActivate = (ClickOpensMenu && nw2.MouseActivate);
			// reallyLostFocus prevents the menu from showing in the case where
			// a message box temporarily caused the control to lose focus, and
			// closing the message box regained the focus. Another possibility
			// is to detect if this control got the focus because its parent window
			// was activated.
			if (!menu.Visible && !isClosing && reallyLostFocus && GotFocusOpensMenu && !mouseActivate && (!CanMirror || !MirrorControlDropDown)) {
				ShowMenu();
			}

			nw2.MouseActivate = false;
		};

		control.LostFocus += delegate {
			if (menu.Focused)
				return;

			if (!menu.Visible)
				return; // this happens when the form is minimized

			IntPtr h1 = GetForegroundWindow();
			bool isTaskbar = IsTaskbar(h1);
			if (isTaskbar)
				return;

			Form f = control.FindForm(); // f might be null if mixing WPF (todo: support WPF)
			reallyLostFocus = (f == null || f.FindActiveControl() != control);
			// Form.ActiveControl essentially maintains the input control that last had the keyboard focus.
			// It seems difficult to determine this using handles. Part of the problem is that when the
			// control regains the focus, first the focus is set to its parent split container, and then
			// the focus is set to the control itself. So it looks like it lost focus to a sibling control
			// and the menu is incorrectly displayed.
			//System.Diagnostics.Debug.WriteLine("control.LostFocus ReallyLostFocus:" + reallyLostFocus + " menu.Focused:" + menu.Focused + " text:" + GetWindowText(h1));
			//if (reallyLostFocus && !KeepMenuOpen && !menu.Focused) {
			//	CloseMenu(ToolStripDropDownCloseReason.CloseCalled);
			//}
			if (!CanMirror || !MirrorControlDropDown) {
				if (reallyLostFocus) {
					// another control on the same window got the focus
					if (AutoCloseReasonControlFocusLost.HasValue)
						CloseMenu(AutoCloseReasonControlFocusLost.Value);
				}
				else {
					//System.Diagnostics.Debug.WriteLine("Control lost focus and window is not top window. AutoCLoseReasonWindowLostFocus: " + AutoCloseReasonControlFocusLost);
					// a different window received the focus
					if (AutoCloseReasonWindowLostFocus.HasValue)
						CloseMenu(AutoCloseReasonWindowLostFocus.Value);
					else {
						if (isClosing)
							return;

						SetWindowPos(Menu.Handle, (IntPtr) 1, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
					}
				}
			}

			// if the focus was transferred to another window, then keep
			// the drop down open. Otherwise if the focus was transfered to
			// another control in the same window then close the menu
			/*if (menu.Visible) {
				bool b = true;
				if (KeepMenuOpen || downKeyPressed) {
					IntPtr fg = GetForegroundWindow();
					IntPtr tl = GetTopWindow(c.Handle);
					b = (tl == fg);
					if (!b) {
						SetWindowPos(menu.Handle, new IntPtr(1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
					}
				}
				if (!menu.Focused && b) {
					CloseMenu(ToolStripDropDownCloseReason.CloseCalled);
				}
			}*/
		};

		control.KeyDown += (o, e) => {
			if (e.KeyData == Keys.Escape && (!CanMirror || !MirrorControlDropDown)) {
				e.Handled = true;
				e.SuppressKeyPress = true; // stops the beep	
				if (menu.Visible && EscapeCloseReason.HasValue && !menu.AutoClose) {
					CloseMenu(EscapeCloseReason.Value);
				}
			}
			//else if (e.KeyData == Keys.Down) {
				// never called
			//}
		};

		control.PreviewKeyDown += (o, e) => {
			if (menu.Visible) {
				if (e.KeyData == Keys.Tab) {
					if (CanMirror && MirrorControlDropDown) {
						nw3.preventClose = true;
						Menu.Focus();
						SelectFirstItem();
						nw3.preventClose = false;
					}
					else {
						Menu.Focus();
						SelectFirstItem();
					}
				}
			}
			else { // menu is currently hidden
				if (!OpenMenuKeys.Contains(e.KeyData))
					return;
					
				downKeyPressed = true;
				if (CanMirror && MirrorControlDropDown) {
					// setting DroppedDown = true handles setting Menu.Focus() and selecting the first item
					if (Control is ComboBox)
						((ComboBox) Control).DroppedDown = true;	
				}
				else {
					if (ShowMenu()) {
						//Menu.Show();
						Menu.Focus();
						SelectFirstItem();
					}
				}

				downKeyPressed = false;
			}
		};

		control.EnabledChanged += delegate {
			menu.Enabled = control.Enabled;
		};

		control.FontChanged += delegate {
			Font f = control.Font;
			Font f2 = menu.Font;
			FontFamily ff = f2.FontFamily;
			// the problem is the control might be a label using Webdings font, so setting the
			// menu's font to Webdings will make all the items unreadable.
			//if (f.FontFamily != oldFont.FontFamily)
			//	ff = f.FontFamily;

			// if the control's font size changes, then it makes sense that the font size
			// of the drop down menu also changes.
			float newSize = Math.Max(MinFontSize, f.Size + FontSizeDelta);
			Font f3 = new Font(ff, newSize, f2.Style, f2.Unit, f2.GdiCharSet, f2.GdiVerticalFont);
			menu.Font = f3;

			if (font != null)
				font.Dispose();

			font = f3;
			oldFont = f;
		};

		// Size and Location typically occur due to a font size change. Repositioning on FontChanged
		// isn't good because a layout will need to be performed, which means it would have to be
		// called on a BeginInvoke.
		control.LocationChanged += delegate { // added 2016-01-25
			if (menu.Visible)
				PositionWindow();
		};

		// SizeChanged could happen due to font 
		control.SizeChanged += delegate { // added 2016-01-25
			if (menu.Visible)
				PositionWindow();
		};
	}

	private static void GetAllControls(Control host, List<Control> list, bool nested, int currentCount, int maxItems) {
		// doesn't make sense to have an option for tabStopOnly because pure ToolStripItem's don't have this property
		// and ToolStripControlHost would have to be set by accessing the Control property.

		foreach (Control c in host.Controls.Cast<Control>().OrderBy(c => c.TabIndex)) {
			if (c.CanSelect) {
				list.Add(c);
				if (currentCount + list.Count >= maxItems)
					break;

				if (c.Controls.Count > 0) // e.g. TextBox with a SnapButton
					GetAllControls(c, list, nested, currentCount, maxItems);
			}
			else if (c.Controls.Count > 0 && nested) {
				GetAllControls(c, list, nested, currentCount, maxItems);
			}
		}
	}

	public void SelectFirstItem() {
		Object o = GetFirstItem();
		SelectItem(o);
	}

	///<summary>Returns the first selectable item, or null if no item can receive the focus (e.g. all ToolStripLabel items).</summary>
	public Object GetFirstItem() {
		int x = 0;
		List<Object> list = new List<Object>();
		GetAllItems(list, Menu.Items, true, ref x, 1);
		return (list.Count > 0 ? list[0] : null);
	}

	public static List<Object> GetAllItems(ToolStripItemCollection items, bool nested, out int selectedIndex) {
		selectedIndex = -1;
		List<Object> list = new List<Object>();
		GetAllItems(list, items, nested, ref selectedIndex);
		return list;
	}

	private static void GetAllItems(List<Object> list, ToolStripItemCollection items, bool nested, ref int selectedIndex, int maxItems = int.MaxValue) {
		for (int i = 0; i < items.Count; i++) {
			ToolStripItem item = items[i];
			if (item.IsOnOverflow) {
				continue; // is this correct?
			}

			// items don't have a TabStop equivalent? So each item with CanSelect is by default a tab-stop.
			if (item is ToolStripControlHost) {
				// e.g. ToolStripTextBox, ToolStripComboBox, or custom
				var host = (ToolStripControlHost) item;
				Control c = host.Control;
				if (item.CanSelect != c.CanSelect) {
				} // would this ever happen?

				List<Control> list2 = new List<Control>();
				if (c.CanSelect) {
					list.Add(item);
					if (list.Count >= maxItems)
						break;
					GetAllControls(c, list2, nested, list.Count, maxItems); // e.g. TextBox with a SnapButton
				}
				else if (nested) {
					GetAllControls(c, list2, nested, list.Count, maxItems);
				}
				foreach (Control c2 in list2) {
					if (c2.Focused)
						selectedIndex = list.Count;
					list.Add(c2);
					if (list.Count >= maxItems)
						break;
				}
				if (list.Count >= maxItems)
					break;
			}
			else if (item is ToolStripDropDownItem) {
				var item2 = (ToolStripDropDownItem) item;
				if (item2.HasDropDownItems) {
					if (nested && item2.DropDown.Visible)
						GetAllItems(list, item2.DropDownItems, nested, ref selectedIndex, maxItems);
				}
				else {
					if (item2.CanSelect) {
						if (item2.Selected)
							selectedIndex = list.Count;
						list.Add(item2);
						if (list.Count >= maxItems)
							break;
					}
				}
			}
			else {
				if (item.CanSelect) {
					if (item.Selected)
						selectedIndex = list.Count;
					list.Add(item);
					if (list.Count >= maxItems)
						break;
				}
			}
		}
	}

	///<summary>Gives the next or previous control the keyboard focus.</summary>
	///<returns>The ToolStripItem or Control that was received the focus.</returns>
	public Object SelectNextItem(bool forward, bool nested, bool wrap) {
		int x = -1;
		List<Object> list = GetAllItems(Menu.Items, nested, out x);
		if (list.Count == 0)
			return null;

for (int i = 0; i < list.Count; i++) {
	System.Diagnostics.Debug.WriteLine(i + " " + list[i]);
}

System.Diagnostics.Debug.WriteLine("selected item: " + x);

		int n = list.Count;
		x = x + (forward ? +1 : -1);
		Object o = null;
		if (x == n) {
			if (wrap)
				o = list[0];
		}
		else if (x < 0) {
			if (wrap)
				o = list[n - 1];
		}
		else
			o = list[x];

		SelectItem(o);
		
		return o;	
	}

	public void SelectItem(Object o) {
		if (o is ToolStripItem) {
			if (o is ToolStripControlHost)
				((ToolStripControlHost) o).Control.Focus(); // calling Select() on ToolStripControlHost doesn't work
			else
				((ToolStripItem) o).Select();
		}
		else if (o is Control)
			((Control) o).Focus(); // Select() ??
		// else null
	}

	private enum CloseReason {
		WindowMinimized,
		AnotherWindowActivated,
		AnotherControlFocused // on the same window
	}

	private static bool IsTaskbar(IntPtr hwnd) {
		bool isTaskbar = (hwnd == FindWindow("Shell_TrayWnd", ""));
		return isTaskbar;
	}

	private class NW3 : NativeWindow {
		public bool preventClose = false;

		private const int WM_KILLFOCUS = 0x8;
		private const int WM_SHOWWINDOW = 0x18;
		protected override void WndProc(ref Message m) {
			//if (preventClose) {
			//	System.Diagnostics.Debug.WriteLine(m);
			//}
			// 0x167 is required when DropDownStyle = DropDown (text is editable)
			// WM_KILLFOCUS is required when DropDownStyle = DropDownList (text is not editable)
			if (m.Msg == 0x167 || m.Msg == WM_KILLFOCUS) {  //|| m.Msg == 0xc1db || m.Msg == 0x167 m.Msg == 0x129
				Debug.WriteLine("NW3 WM_KILLFOCUS preventClose:" + preventClose);
				if (preventClose)
					return;
			}
			base.WndProc(ref m);
		}
	}

	private class NW2: NativeWindow {
		public bool MouseActivate = false;
		private const int WM_MOUSEACTIVATE = 0x0021;
		private const int WM_MOUSEUP = 0x202;
		private const int WM_MOUSEDOWN = 0x201;
		public event MouseEventHandler MouseUp;
		public NW2() {}

		protected override void WndProc(ref Message m) {
			base.WndProc(ref m);
			if (m.Msg == WM_MOUSEACTIVATE)
				MouseActivate = true;
			else if (m.Msg == WM_MOUSEUP) {
				if (MouseUp != null)
					MouseUp(null, null);
			}
		}
	}

	private class NW : NativeWindow {

		private const int SW_RESTORE = 9;
		private const int SW_SHOW = 5;
		private const int WM_ACTIVATEAPP = 0x001C;
		private const int WM_ACTIVATE = 0x6;

		ToolStripDropDownAttacher attacher = null;

		public NW(ToolStripDropDownAttacher attacher) {
			this.attacher = attacher;
			AssignHandle(attacher.Menu.Handle);
		}

public static readonly IList<int> ignoreList = new[] { 0x133, 0xd, 0xe, 0xf, 0x282, 0x281, 0x46, 0x47, 0x14, 0x85, 0x111, 0xc1e8, 0x2e0, 0x55, 0x129, 0x210, 0x200, 0x2a3, 0x118,
0xc0ab, 0x113, 0xa0, 0x2a2, 0x2a1, 0x31f, 0x5, 0x83, 0x84, 0x400, 0x20, 0x820712, 0xc1a9, 0x87, 0xc260, 0xd00c6, 0xc1e4, 0x3, 0x20a, 0x20e
 };

		protected override void WndProc(ref Message m) {
//if (!ignoreList.Contains(m.Msg))
//	Debug.WriteLine("menu: " + m);

			if (m.Msg == WM_ACTIVATE) {
				HideMenu(attacher);
//Form f = attacher.Control.FindForm();
//System.Diagnostics.Debug.WriteLine("Form state: " + f.WindowState);
//var wp = new WINDOWPLACEMENT();
//GetWindowPlacement(f.Handle, ref wp);
//System.Diagnostics.Debug.WriteLine("WP: " + wp.showCmd);
			

			}
			//else if (attacher.wasMinimized) {
			//	if (m.Msg == WM_ACTIVATEAPP && m.WParam.ToInt32() == 1) {
			//		//attacher.wasMinimized = false;
			//		Debug.WriteLine(m);
			//		ShowWindow(Handle, SW_SHOW);
			//	}
			//}

			else if (m.Msg == WM_ACTIVATEAPP) {
System.Diagnostics.Debug.WriteLine("WM_ACTIVATEAPP WParam: " + m.WParam + " Menu.Visible: " + attacher.Menu.Visible + " IsAdjusting: " + attacher.isAdjusting + " WasMinimized: " + attacher.wasMinimized);

				if (m.WParam.ToInt32() == 0) {
System.Diagnostics.Debug.WriteLine("WM_ACTIVATEAPP: window deactivated, hiding menu");
					HideMenu(attacher);
				}
				else if (attacher.wasMinimized && m.WParam.ToInt32() == 1) {
					attacher.wasMinimized = false;
					attacher.isAdjusting = true;				
					ShowWindow(attacher.Menu.Handle, SW_SHOW);
					attacher.isAdjusting = false;
					//try {
					//attacher.Menu.BeginInvoke((Action) delegate {
					//	try {
					//		attacher.ShowMenu();
					//	} catch {}
					//});
					//} catch {}
				}
			}
			base.WndProc(ref m);
		}

		private static void HideMenu(ToolStripDropDownAttacher attacher) {
			if (!attacher.isAdjusting && attacher.Menu.Visible) {
				// this code is required to hide the menu when the focus is lost in some cases, e.g:
				// -a different top level window receives the focus
				// -the top level window hosting the control is minimized
				bool hideMenu = false;

				IntPtr h1 = GetForegroundWindow();
				IntPtr h2 = GetTopWindow(attacher.Control.Handle);
				bool b1 = (h1 == attacher.Menu.Handle);
				bool b2 = (h1 == attacher.Control.Handle); // probably never happens
				bool b3 = (h1 == h2);
				if (!b1 && !b2 && !b3) {
					hideMenu = true;
					// is the window still visible or minimized?
					//bool b = IsWindowVisible(h2);
					//System.Diagnostics.Debug.WriteLine("window visible: " + b);
				}
				else {
				}
				Debug.WriteLine("hideMenu: " + hideMenu);
				if (hideMenu) {
					// doing this results in multiple events WM_ACTIVATE as the window battles to lose focus:
					//attacher.isClosing = true;  // don't
					//attacher.Control.Focus();   // do
					//attacher.isClosing = false; // this
					//int z2 = GetWindowZOrder(attacher.Control.Handle);
					if (attacher.AutoCloseReasonWindowLostFocus.HasValue) {
						ShowWindow(attacher.Menu.Handle, SW_HIDE); // must also hide or floating button remains
						attacher.lastClosedReason = CloseReason.AnotherWindowActivated;
						attacher.wasMinimized = true;
					}
					else {
						//int z = GetWindowZOrder(h2);
						SetWindowPos(attacher.Menu.Handle, (IntPtr) 1, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
					}
				
					//ShowWindow(Handle, SW_PARENTCLOSING);
					//ShowWindow(Handle, SW_MINIMIZE);
					//System.Diagnostics.Debug.WriteLine("wasMinimized: " + m + " zorder: " + z + "   " + z2);
				}
			}
		}
	}

	private static int GetWindowZOrder(IntPtr hWnd) {
		const int HWNDPREV = 3;
		var z = 0;
		for (var h = hWnd; h != IntPtr.Zero; h = GetWindow(h, HWNDPREV))
			z++;

		return z;
	}

	private static bool IsAttacherForegroundWindow(ToolStripDropDownAttacher attacher) {
		IntPtr h1 = GetTopWindow(attacher.Control.Handle);
		IntPtr h2 = GetForegroundWindow();
		return h1 == h2;
	}

	private bool CanMirror {
		get {
			return nw3 != null;
		}
	}

	public bool PreFilterMessage(ref Message m) {
//if (!NW.ignoreList.Contains(m.Msg))
//Debug.WriteLine("attacher:" + DateTime.Now.ToString("HH:mm:ss:") + m);
		// seems like PreFilterMessage is the only way to prevent the focus from transferring to the next control
		// on the same main window. The TAB key is never fired in the c.KeyDown event, and trying to use
		// PreviewKeyDown doesn't give an option to block the key (unlike overriding the ProcessCmdKey method).
		if (m.Msg == WM_KEYDOWN) {
			int vk = m.WParam.ToInt32();
			if (vk == VK_TAB) {
				if (Control.IsHandleCreated && Menu.Visible && Menu.TabStop) {
					bool b = (m.HWnd == Control.Handle);
					if (!b) {
						Control c2 = Control.FromChildHandle(m.HWnd);
						b = (c2 != null && c2.IsHandleCreated && c2.Handle == Control.Handle);
					}
					if (b) {
						isFocusing = true;
						Menu.Focus();
						isFocusing = false;
						return true;
					}
				}
			}
			else if (vk == VK_DOWN || vk == VK_UP) {
				if (Control.IsHandleCreated && m.HWnd == Control.Handle && Menu.TabStop) {
				}	
			}
			else if (vk == VK_ESCAPE) {
				if (Control.IsHandleCreated && EscapeCloseReason.HasValue && Menu.Visible && !Menu.AutoClose) {
					if (CanMirror && MirrorControlDropDown) {
						isClosing = true;
						Control.Focus();
						isClosing = false;
						if (Control is ComboBox)
							((ComboBox) Control).DroppedDown = false;
					}
					else {
						isClosing = true;
						Control.Focus();
						isClosing = false;
						// Some dialogs automatically close when the escape key is pressed. But a more friendly behavior is to first close the
						// menu (if it is open), and then a second escape key will close the dialog. Note: it doesn't matter which control
						// currently has the focus. If multiple menus are open, then an escape key would be required for each one.
						CloseMenu(EscapeCloseReason.Value);
						return true;
					}
				}
			}
		}
		return false;
	}

	public void CloseMenu(ToolStripDropDownCloseReason reason) {
		if (wasMinimized) {
		}
		if (CanMirror && MirrorControlDropDown) {
			if (Control is ComboBox) {
				ComboBox combo = (ComboBox) Control;
				if (combo.DroppedDown) {
System.Diagnostics.Debug.WriteLine("setting dorpped down flase");
					combo.DroppedDown = false;
				}
				else {
System.Diagnostics.Debug.WriteLine("CloseMenu(" + reason + ")");
					isClosing = true;
					Menu.Close(reason);
					isClosing = false;
				}
			}
		}
		else {
			isClosing = true;
			Menu.Close(reason);
			isClosing = false;
		}
	}

	public bool ShowMenu() {
		if (MenuShowing != null) {
			CancelEventArgs e = new CancelEventArgs();
			MenuShowing(this, e);
			if (e.Cancel)
				return false;
		}

		IntPtr hWnd = GetTopWindow(Control.Handle);
		int factor1 = (Alignment == HorizontalAlignment.Left ? 0 : -1);
		int factor2 = (Alignment == HorizontalAlignment.Left ? 0 : 1);
		Menu.SnapWindow(Control.Handle, hWnd, new SnapPoint { ParentHeightFactor = 1, OffsetConstantY = 1, ChildWidthFactor = factor1, ParentWidthFactor = factor2 });
		PositionWindow();
		return true;
	}



	//---------------------------------------------------
	  
	private void PositionWindow() {
		RECT r = new RECT();
		GetWindowRect(Control.Handle, ref r);
		// Show(Control, Point) is indented by the control's border, e.g. a TextBox has an internal
		// border of 2, so the control needs to be shifted left. This inconsistency makes using
		// Show(Control, Point) no good, so GetWindowRect is used instead.
		// Menu.Show(this, new Point(-2, this.Height)); <-- no good
		Point pt = (Alignment == HorizontalAlignment.Left ? new Point(r.Left, r.Bottom + 1) : new Point(r.Right - Menu.Width, r.Bottom + 1));
		Menu.Show(pt);
		// Required to set the control back on top (e.g. click off the window, then click back on the
		// window, the ClockMenu will appear underneath).
		// if SWP_NOACTIVATE is not used, then the main window will lose the focus when this drop down is closed.
		// this is similar to Control.BringToFront(), but BringToFront doesn't use the SWP_NOACTIVATE flag.
		SetWindowPos(Menu.Handle, (IntPtr) 0, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
	}

	private static IntPtr GetTopWindow(IntPtr hWnd) {
		while (true) {
			IntPtr p2 = GetParent(hWnd);
			// some windows return their own handle if they are topmost
			if (p2 == hWnd || p2 == IntPtr.Zero)
				break;
			hWnd = p2;
		}
		return hWnd;
	}

	private static RECT GetWindowRect(IntPtr hWnd) {
		RECT r = new RECT();
		GetWindowRect(hWnd, ref r);
		return r;
	}

	private const int WM_NCACTIVATE = 0x86;
	private const int WM_KEYDOWN = 0x100;
	private const int VK_TAB = 0x09;
	private const int VK_UP = 0x26;
	private const int VK_DOWN = 0x28;
	private const int VK_ESCAPE = 0x1B;

	private const int SWP_NOSIZE = 0x0001;
	private const int SWP_NOACTIVATE = 0x0010;
	private const int SWP_NOMOVE = 0x0002;

	private const int WM_SHOWWINDOW = 0x0018;
	private const int SW_PARENTCLOSING = 1;
	private const int SW_HIDE = 0;
	//private const int SW_MINIMIZE = 6;
	private const int GW_OWNER = 4;

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, ref RECT lpRect);

	[DllImport("user32.dll")]
	private static extern void SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern IntPtr GetParent(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

	[DllImport("user32.dll")]
	private static extern int ShowWindow(IntPtr hWnd, int cmdShow);

	private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd) {
		WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
		placement.length = Marshal.SizeOf(placement);
		GetWindowPlacement(hwnd, ref placement);
		return placement;
	}

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

	[StructLayout(LayoutKind.Sequential)]
	private struct WINDOWPLACEMENT {
		public int length;
		public int flags;
		public ShowWindowCommands showCmd;
		public System.Drawing.Point ptMinPosition;
		public System.Drawing.Point ptMaxPosition;
		public System.Drawing.Rectangle rcNormalPosition;
	}

	private enum ShowWindowCommands : int {
		Hide = 0,
		Normal = 1,
		Minimized = 2,
		Maximized = 3,
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
	private static String GetWindowText(IntPtr hWnd) {
		StringBuilder sb = new StringBuilder(256);
		GetWindowText(hWnd, sb, sb.Capacity);
		return sb.ToString();
	}

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(String className, String windowText);
}

}