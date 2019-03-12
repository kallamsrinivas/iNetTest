using System.Collections.Generic;


namespace ISC.iNet.DS.Services
{
    ////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality for displaying docking station status.
    /// </summary>
    public sealed partial class ConsoleService : Service
    {
        /// <summary>
        /// The class used to represent a menu on the IDS.
        /// </summary>
        private class Menu
        {
            #region Fields

            /// <summary>
            /// The delegate for the function used to display the menu.
            /// </summary>
            public delegate void DisplayFunction();

            /// <summary>
            /// The list of menu items on this menu.
            /// </summary>
            private List<MenuItem> _menuItems;

            /// <summary>
            /// The function used to display this menu.
            /// </summary>
            private DisplayFunction _displayFunction;

            /// <summary>
            /// Which menu item is currently selected.
            /// </summary>
            private int _selected;

            #endregion

            #region Properties

            /// <summary>
            /// The menu items that are on this menu, ordered from top to bottom.
            /// </summary>
            internal List<MenuItem> MenuItems
            {
                get
                {
                    if ( _menuItems == null )
                        _menuItems = new List<MenuItem>();

                    return _menuItems;
                }
                set
                {
                    _menuItems = value;
                }
            }

            /// <summary>
            /// Determine which menu item is selected.
            /// </summary>
            internal int Selected
            {
                get
                {
                    return _selected;
                }
                set
                {
                    _selected = value;
                }
            }

            #endregion

            #region Constructors

            /// <summary>
            /// Construct a new Menu object with the default display method.
            /// </summary>
            public Menu()
            {
                _displayFunction = null;
                _selected = -1;
            }

            /// <summary>
            /// Construct a new Menu object with a special diaply function.
            /// </summary>
            /// <param name="displayFunction"></param>
            public Menu( DisplayFunction displayFunction )
            {
                _displayFunction = displayFunction;
                _selected = -1;
            }

            #endregion

            #region Methods

            /// <summary>
            /// Move the selection in the menu up.
            /// </summary>
            public void MoveSelectionUp()
            {
                int i;

                //	If the current selection is greater than 0 and the menu item above
                //	it is selectable, select it.
                if ( _selected > 0
                    && ( (MenuItem)_menuItems[ _selected - 1 ] ).Selectable )
                {
                    _selected--;
                }
                else
                {
                    //	Start with the currently selected item.
                    i = _selected - 1;

                    //	While we are still on the menu and the previous item
                    //	is not selectable.
                    while ( ( i > 0 ) && !( (MenuItem)_menuItems[--i] ).Selectable ) { /* do nothing */ }

                    //	If we found a menu item that is selectable, use it,
                    //	otherwise ignore it.
                    if ( ( i >= 0 ) && ( (MenuItem)_menuItems[i] ).Selectable )
                    {
                        _selected = i;
                    }

                    // If highlighted selection reaches the top of the menu, it should cycle around to the bottom.
                    // So, if we didn't find anything that is selectable, then we start at the very bottom of the
                    // menu and scan upwards until we find a selectable item.  We'll then either end up at the
                    // bottom-most selectable item, or else the currently selected item.
                    else
                    {
                        i = _menuItems.Count - 1;
                        while ( i >= _selected && !( (MenuItem)_menuItems[i--] ).Selectable ) { /* do nothing */ }
                        _selected = i + 1;  // add one since the loop will have gone one too far.
                    }
                }
            }

            /// <summary>
            /// Move the current selection  down.
            /// </summary>
            public void MoveSelectionDown()
            {
                int i;

                //	If the next selection is on the menu and selectable, select it.
                if ( ( _selected < _menuItems.Count - 1 )
                    && ( (MenuItem)_menuItems[ _selected + 1 ] ).Selectable )
                {
                    _selected++;
                }
                else
                {
                    //	Start with the current selection.
                    i = _selected + 1;

                    //	While still on the menu and the next item is not selectable.
                    while ( ( i < _menuItems.Count - 1 )
                        && !( (MenuItem)_menuItems[ ++i ] ).Selectable ) { /* do nothing */ }

                    //	If the new selection is valid, update the selection.
                    if ( ( i < _menuItems.Count )
                        && ( (MenuItem)_menuItems[i] ).Selectable )
                    {
                        _selected = i;
                    }

                    // If highlighted selection reaches the bottom of the menu, it should cycle around to the top.
                    // So, If we didn't find anything that is selectable, then we start at the very top of the
                    // menu and scan downwards until we find a selectable item.  We'll then either end up at
                    // the first-most selectable item, or else the currently selected item.
                    else
                    {
                        i = 0;
                        while ( i <= _selected && !( (MenuItem)_menuItems[i++] ).Selectable ) { /* do nothing */ }
                        _selected = i - 1;  // backup one since the loop will have gone one too far.
                    }
                }
            }

            /// <summary>
            /// Activate the currently selected menu item.
            /// </summary>
            /// <returns>The menu that is supposed to be the next current menu.</returns>
            public Menu Activate()
            {
                //	If the selection is valid.
                if ( _selected >= 0 )
                {
                    //	Run its activate function.
                    ( (MenuItem)_menuItems[ _selected ] ).Activate();

                    //	Return its destination menu.
                    return ( (MenuItem)_menuItems[ _selected ] ).Destination;
                }
                else
                {
                    //	Otherwise return a null destination.
                    return null;
                }
            }

            //	TODO:	Add support for a menu that is too long for the screen.
            //	TODO:	Add support for wrapping around to the top from the bottom, etc.
            //	TODO:	Add multi-line formatting for messages that are too long to
            //			fit on the screen. This will affect the menu display function
            //			for menus that are too log to fit on the screen.
            /// <summary>
            /// Display the current menu to the LCD.
            /// </summary>
            public void Display()
            {
                string content;

                // If the menu has a special display function, use it.
                if ( _displayFunction != null )
                {
                    _displayFunction();
                }
                else
                {
                    content = "";

                    // If we have no current selection, move it down until it
                    // finds a valid selection.
                    if ( _selected == -1 )
                    {
                        MoveSelectionDown();
                    }

                    // If it could not find a valid selection, leave.
                    if ( _selected == -1 )
                    {
                        return;
                    }

                    // Cycle through the menu's menu items.
                    for ( int i = 0; i < _menuItems.Count; i++ )
                    {
                        // It the menu item is not visible, ignore it.
                        if ( !( (MenuItem)_menuItems[ i ] ).Visible )
                        {
                            continue;
                        }
                        // If it is selected, make it inverse.
                        else if ( _selected == i )
                        {
                            content += "<b>" + ( (MenuItem)_menuItems[ i ] ).Text + "</b>";
                        }
                        // Otherwise display it normally.
                        else
                        {
                            content += "<a>" + ( (MenuItem)_menuItems[ i ] ).Text + "</a>";
                        }
                    }

                    DS.LCD.Display( content ); // Write the menu to the LCD.
                }
            }

            #endregion
        }

        /////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The class used to represent a single menu item within a menu on the LCD.
        /// </summary>
        private class MenuItem
        {

            #region Fields

            /// <summary>
            /// The delegate used for a menu item's specialized visibility function.
            /// </summary>
            public delegate bool VisibleFunction();

            /// <summary>
            /// The delegate used for a menu item's activation function.
            /// </summary>
            public delegate void ActivateFunction();

            /// <summary>
            /// The text for the menu item.
            /// </summary>
            private string _text;

            /// <summary>
            /// Whether or not the menu item is selectable.
            /// </summary>
            private bool _selectable;

            /// <summary>
            /// Is this menu item selected?
            /// </summary>
            private bool _selected;

            /// <summary>
            /// The destination menu that should be displayed after the activate
            /// function has been executed.
            /// </summary>
            private Menu _destination;

            /// <summary>
            /// The method used to the determine if this menu item is visible.
            /// </summary>
            private VisibleFunction _visibleFunction;

            /// <summary>
            /// The method called when this menu item is activated.
            /// </summary>
            private ActivateFunction _activateFunction;

            #endregion

            #region Constructors

            /// <summary>
            /// Constructs a new menu item object.
            /// </summary>
            /// <param name="text">The text to be shown on the LCD.</param>
            /// <param name="selectable">Is this menu item selectable?</param>
            /// <param name="visibleFunction">The function used to determine
            ///		if this menu item is visible.</param>
            /// <param name="activateFunction">The function used when this menu
            ///		item is activated.</param>
            /// <param name="destination">The menu that is the destination after
            ///		this menu item is activated.</param>
            public MenuItem( string text, bool selectable, VisibleFunction visibleFunction,
                ActivateFunction activateFunction, Menu destination )
            {
                _text = text;
                _selectable = selectable;
                _visibleFunction = visibleFunction;
                _activateFunction = activateFunction;
                _destination = destination;
            }

            #endregion

            #region Properties

            /// <summary>
            /// The text to be displayed for this menu item.
            /// </summary>
            public string Text
            {
                get
                {
                    return _text;
                }
            }

            /// <summary>
            /// Is this menu item selectable?
            /// </summary>
            public bool Selectable
            {
                get
                {
                    return _selectable && Visible;
                }
            }

            /// <summary>
            /// Is this menu item currently selected?
            /// </summary>
            public bool Selected
            {
                get
                {
                    return _selected;
                }
                set
                {
                    _selected = value;
                }
            }

            /// <summary>
            /// Is this menu item visible?
            /// </summary>
            public bool Visible
            {
                get
                {
                    // If there is a custom visibility function, use it.
                    if ( _visibleFunction != null )
                    {
                        return _visibleFunction();
                    }
                    else
                    {
                        // Otherwise, assume that it is visible.
                        return true;
                    }
                }
            }

            /// <summary>
            /// The destination menu that should be displayed after this
            ///	menu item is activated.
            /// </summary>
            public Menu Destination
            {
                get
                {
                    return _destination;
                }
            }

            #endregion

            #region Methods

            /// <summary>
            /// Execute this menu item's activation function.
            /// </summary>
            public void Activate()
            {
                // If this menu item has an activation function, execute it.
                if ( _activateFunction != null )
                {
                    _activateFunction();
                }
            }

            #endregion
        }
    }
}
